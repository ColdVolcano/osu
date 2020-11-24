// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Batches;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.OpenGL.Vertices;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Utils;
using osu.Framework.Extensions.Color4Extensions;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// A visualiser that reacts to music coming from beatmaps.
    /// </summary>
    public class LogoVisualisation : Drawable
    {
        private readonly IBindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        /// <summary>
        /// The maximum length of each bar in the visualiser. Will be reduced when kiai is not activated.
        /// </summary>
        private const float bar_length = 600;

        /// <summary>
        /// How much should each bar go down each millisecond (based on a full bar).
        /// </summary>
        private const float decay_per_milisecond = 0.0024f;

        /// <summary>
        /// Number of milliseconds between each amplitude update.
        /// </summary>
        private const float time_between_updates = 50;

        /// <summary>
        /// The minimum amplitude to show a bar.
        /// </summary>
        private const float amplitude_dead_zone = 1f / bar_length;

        /// <summary>
        /// The number of bars to jump each update iteration.
        /// </summary>
        public int IndexChange { get; set; } = 5;

        /// <summary>
        /// The relative movement of bars based on input amplification. Defaults to 1.
        /// </summary>
        public float Magnitude { get; set; } = 1;

        /// <summary>
        /// Maximum number of bars that can occupy a single visualiser position.
        /// If there are more bars in one position than this, only the longer ones will be rendered.
        /// </summary>
        public int MaxBarsPerPosition { get; set; } = 4;

        /// <summary>
        /// The number of bars in one rotation of the visualiser.
        /// </summary>
        public int PositionsPerVisualiserRound { get; set; } = 200;

        /// <summary>
        /// How many times we should stretch around the circumference (overlapping overselves).
        /// </summary>
        public int VisualiserRounds { get; set; } = 5;

        private readonly float[] frequencyAmplitudes = new float[256];

        private int indexOffset;

        private IShader shader;
        private Texture texture = Texture.WhitePixel;

        public Texture Texture
        {
            get => texture;
            set
            {
                if (value == null)
                {
                    texture = Texture.WhitePixel;
                    return;
                }

                texture = value;
            }
        }

        public LogoVisualisation()
        {
            Blending = BlendingParameters.Additive;
        }

        private readonly List<IHasAmplitudes> amplitudeSources = new List<IHasAmplitudes>();

        public void AddAmplitudeSource(IHasAmplitudes amplitudeSource)
        {
            amplitudeSources.Add(amplitudeSource);
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, IBindable<WorkingBeatmap> beatmap)
        {
            this.beatmap.BindTo(beatmap);
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
        }

        private readonly float[] temporalAmplitudes = new float[ChannelAmplitudes.AMPLITUDES_SIZE];

        private void updateAmplitudes()
        {
            var effect = beatmap.Value.BeatmapLoaded && beatmap.Value.TrackLoaded
                ? beatmap.Value.Beatmap?.ControlPointInfo.EffectPointAt(beatmap.Value.Track.CurrentTime)
                : null;

            for (int i = 0; i < temporalAmplitudes.Length; i++)
                temporalAmplitudes[i] = 0;

            if (beatmap.Value.TrackLoaded)
                addAmplitudesFromSource(beatmap.Value.Track);

            foreach (var source in amplitudeSources)
                addAmplitudesFromSource(source);

            for (int i = 0; i < PositionsPerVisualiserRound; i++)
            {
                float targetAmplitude = (temporalAmplitudes[(i + indexOffset) % PositionsPerVisualiserRound]) * (effect?.KiaiMode == true ? 1 : 0.5f);
                if (targetAmplitude > frequencyAmplitudes[i])
                    frequencyAmplitudes[i] = targetAmplitude;
            }

            indexOffset = (indexOffset + IndexChange) % PositionsPerVisualiserRound;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var delayed = Scheduler.AddDelayed(updateAmplitudes, time_between_updates, true);
            delayed.PerformRepeatCatchUpExecutions = false;
        }

        protected override void Update()
        {
            base.Update();

            float decayFactor = (float)Time.Elapsed * decay_per_milisecond;

            for (int i = 0; i < PositionsPerVisualiserRound; i++)
            {
                //3% of extra bar length to make it a little faster when bar is almost at it's minimum
                frequencyAmplitudes[i] -= decayFactor * (frequencyAmplitudes[i] + 0.03f);
                if (frequencyAmplitudes[i] < 0)
                    frequencyAmplitudes[i] = 0;
            }

            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new VisualisationDrawNode(this);

        private void addAmplitudesFromSource([NotNull] IHasAmplitudes source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var amplitudes = source.CurrentAmplitudes.FrequencyAmplitudes.Span;

            for (int i = 0; i < amplitudes.Length; i++)
            {
                if (i < temporalAmplitudes.Length)
                    temporalAmplitudes[i] += amplitudes[i];
            }
        }

        private class VisualisationDrawNode : DrawNode
        {
            protected new LogoVisualisation Source => (LogoVisualisation)base.Source;

            private IShader shader;
            private Texture texture;

            // Assuming the logo is a circle, we don't need a second dimension.
            private float size;
            private int positions;
            private int rounds;
            private float magnitude;
            private int maxBarsPerPosition;

            private static readonly Color4 transparent_white = Color4.White.Opacity(0.2f);

            private float[] audioData;

            private readonly QuadBatch<TexturedVertex2D> vertexBatch = new QuadBatch<TexturedVertex2D>(100, 10);

            public VisualisationDrawNode(LogoVisualisation source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                size = Source.DrawSize.X;
                audioData = Source.frequencyAmplitudes;
                positions = Source.PositionsPerVisualiserRound;
                rounds = Source.VisualiserRounds;
                magnitude = Source.Magnitude;
                maxBarsPerPosition = Source.MaxBarsPerPosition;
            }

            public override void Draw(Action<TexturedVertex2D> vertexAction)
            {
                base.Draw(vertexAction);

                shader.Bind();

                Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                if (audioData != null)
                {
                    float barWidth = size * MathF.Sqrt(2 * (1 - MathF.Cos(MathUtils.DegreesToRadians(360f / positions)))) / 2f;
                    float anglePerPosition = 360f / positions;

                    float[] dataInPosition = new float[maxBarsPerPosition];

                    ColourInfo colourInfo = DrawColourInfo.Colour;
                    colourInfo.ApplyChild(transparent_white);

                    for (int i = 0; i < positions; i++)
                    {
                        float rotation = MathUtils.DegreesToRadians(i * anglePerPosition);
                        float rotationCos = MathF.Cos(rotation);
                        float rotationSin = MathF.Sin(rotation);

                        // taking the cos and sin to the 0..1 range
                        var barPosition = new Vector2(MathF.Cos(rotation) / 2 + 0.5f, MathF.Sin(rotation) / 2 + 0.5f) * size;

                        // The distance between the position and the sides of the bar.
                        var bottomOffset = new Vector2(-rotationSin * barWidth / 2, rotationCos * barWidth / 2);

                        for (int j = 0; j < dataInPosition.Length; j++)
                            dataInPosition[j] = 0;

                        //picking the longest bars that would fall into this specific rotation...
                        for (int j = 0; j < rounds; j++)
                        {
                            float targetData = audioData[(i + j * positions / rounds) % positions] * magnitude;

                            if (targetData < amplitude_dead_zone)
                                continue;

                            for (int d = 0; d < dataInPosition.Length; d++)
                            {
                                if (!(targetData > dataInPosition[d])) continue;

                                for (int l = dataInPosition.Length - 1; l > d; l--)
                                    dataInPosition[l] = dataInPosition[l - 1];
                                dataInPosition[d] = targetData;
                                break;
                            }
                        }

                        foreach (float d in dataInPosition)
                        {
                            var barHeight = bar_length * d;

                            // The distance between the bottom side of the bar and the top side.
                            var amplitudeOffset = new Vector2(rotationCos * barHeight, rotationSin * barHeight);

                            var rectangle = new Quad(
                                Vector2Extensions.Transform(barPosition - bottomOffset + amplitudeOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition + bottomOffset + amplitudeOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition - bottomOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition + bottomOffset, DrawInfo.Matrix)
                            );

                            DrawQuad(
                                texture,
                                rectangle,
                                colourInfo,
                                null,
                                vertexBatch.AddAction,
                                Vector2.Divide(inflation, rectangle.Size));
                        }
                    }
                }

                shader.Unbind();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                vertexBatch.Dispose();
            }
        }
    }
}
