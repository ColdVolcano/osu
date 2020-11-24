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
        /// The number of bars to jump counterclockwise each update iteration.
        /// </summary>
        public int IndexChange { get; set; } = 5;

        /// <summary>
        /// The number of bars in one rotation of the visualiser.
        /// </summary>
        public int BarPositions { get; set; } = 200;

        /// <summary>
        /// Static rotation applied to each bar position to give the appearance of tilting.
        /// </summary>
        public float BarRotation { get; set; }

        /// <summary>
        /// Position at which the first bar is rendered, relative to the centre right position.
        /// Positive values will move the first bar counterclockwise.
        /// </summary>
        public int StartPositionDisplacement { get; set; }

        /// <summary>
        /// The relative movement of bars based on input amplification. Defaults to 1.
        /// </summary>
        public float Magnitude { get; set; } = 1;

        /// <summary>
        /// Maximum number of bars that can occupy a single visualiser position.
        /// If there are more bars in one position than this, only the longer ones will be rendered.
        /// Alpha of a fully occupied position will be 1.
        /// </summary>
        public int MaxBarsPerPosition { get; set; } = 4;

        /// <summary>
        /// Continue rendering bars for a given visualiser round after it has filled all possible positions, overlapping itself in the process.
        /// This has no effect if <see cref="BarPositions"/> is greater than or equal to <see cref="ChannelAmplitudes.AMPLITUDES_SIZE"/>.
        /// </summary>
        public bool ShowFullSpectrum { get; set; }

        /// <summary>
        /// How many times we should stretch around the circumference (overlapping overselves).
        /// </summary>
        public int VisualiserRounds { get; set; } = 5;

        public int BarsPerVisualiserRound => ShowFullSpectrum ? ChannelAmplitudes.AMPLITUDES_SIZE : Math.Min(BarPositions, ChannelAmplitudes.AMPLITUDES_SIZE);

        private readonly float[] frequencyAmplitudes = new float[ChannelAmplitudes.AMPLITUDES_SIZE];

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

            for (int i = 0; i < temporalAmplitudes.Length; i++)
            {
                int targetIndex = ((i + indexOffset + StartPositionDisplacement) % BarsPerVisualiserRound + BarsPerVisualiserRound) % BarsPerVisualiserRound;
                float targetAmplitude = temporalAmplitudes[targetIndex] * (effect?.KiaiMode == true ? 1 : 2 / 3f) * Magnitude;
                if (targetAmplitude > frequencyAmplitudes[i])
                    frequencyAmplitudes[i] = targetAmplitude;
            }

            if (IndexChange != 0)
                indexOffset = (indexOffset + IndexChange) % BarsPerVisualiserRound;
            else
                indexOffset = 0;
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

            for (int i = 0; i < frequencyAmplitudes.Length; i++)
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

        public void ApplyCirclesStyle()
        {
            IndexChange = 5;
            BarPositions = 256;
            BarRotation = 0;
            ShowFullSpectrum = false;
            StartPositionDisplacement = 0;
            Magnitude = 1;
            MaxBarsPerPosition = 4;
            VisualiserRounds = 5;
        }

        public void ApplyWelcomeStyle()
        {
            IndexChange = 0;
            BarPositions = 128;
            BarRotation = 0;
            ShowFullSpectrum = true;
            StartPositionDisplacement = 74;
            Magnitude = 1;
            MaxBarsPerPosition = 2;
            VisualiserRounds = 1;
        }

        public void ApplyTrianglesStyle()
        {
            IndexChange = 6;
            BarPositions = 256;
            BarRotation = 0;
            ShowFullSpectrum = false;
            StartPositionDisplacement = 0;
            Magnitude = 1;
            MaxBarsPerPosition = 3;
            VisualiserRounds = 5;
        }

        public void ApplyTiltedTrianglesStyle()
        {
            IndexChange = 6;
            BarPositions = 256;
            BarRotation = -1.5f;
            ShowFullSpectrum = false;
            StartPositionDisplacement = -2;
            Magnitude = 0.5f;
            MaxBarsPerPosition = 3;
            VisualiserRounds = 5;
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
            private int maxBarsPerPosition;
            private int barsPerRound;
            private float barRotation;

            private Color4 transparentWhite => Color4.White.Opacity(1f / maxBarsPerPosition);

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
                positions = Source.BarPositions;
                rounds = Source.VisualiserRounds;
                maxBarsPerPosition = Source.MaxBarsPerPosition;
                barsPerRound = Source.BarsPerVisualiserRound;
                barRotation = Source.BarRotation;
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
                    colourInfo.ApplyChild(transparentWhite);

                    for (int i = 0; i < positions; i++)
                    {
                        float positionalRotation = MathUtils.DegreesToRadians(i * anglePerPosition);
                        float visualRotation = MathUtils.DegreesToRadians(barRotation) + positionalRotation;
                        float rotationCos = MathF.Cos(visualRotation);
                        float rotationSin = MathF.Sin(visualRotation);

                        // taking the cos and sin to the 0..1 range
                        var barPosition = new Vector2(MathF.Cos(positionalRotation) / 2 + 0.5f, MathF.Sin(positionalRotation) / 2 + 0.5f) * size;

                        // The distance between the position and the sides of the bar.
                        var bottomOffset = new Vector2(-rotationSin * barWidth / 2, rotationCos * barWidth / 2);

                        for (int j = 0; j < dataInPosition.Length; j++)
                            dataInPosition[j] = 0;

                        //picking the longest bars that would fall into this specific rotation...
                        for (int j = 0; j < rounds; j++)
                        {
                            for (int k = (i + j * positions / rounds) % barsPerRound; k < barsPerRound; k += positions)
                            {
                                float targetData = audioData[k];

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
