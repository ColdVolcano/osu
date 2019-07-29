// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Batches;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.OpenGL.Vertices;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Menu
{
    public class LogoVisualisation : Drawable, IHasAccentColour
    {
        private readonly IBindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        /// <summary>
        /// The number of bars to jump each update iteration.
        /// </summary>
        private const int index_change = 6;

        /// <summary>
        /// The maximum length of each bar in the visualiser. Will be reduced when kiai is not activated.
        /// </summary>
        private const float bar_length = 600;

        /// <summary>
        /// The number of bars in one rotation of the visualiser.
        /// </summary>
        private const int bars_per_visualiser = 256;

        /// <summary>
        /// How many times we should stretch around the circumference (overlapping overselves).
        /// </summary>
        private const float visualiser_rounds = 5;

        /// <summary>
        /// How much should each bar go down each second (relative to a full bar).
        /// </summary>
        private const float decay_per_second = 2.6f;

        /// <summary>
        /// Number of milliseconds between each index shift.
        /// </summary>
        private const float index_shift_delay = 50;

        /// <summary>
        /// The minimum amplitude to show a bar.
        /// </summary>
        private const float amplitude_dead_zone = 2f / bar_length;

        private int indexOffset;

        public Color4 AccentColour { get; set; }

        private readonly float[] frequencyAmplitudes = new float[256];

        private Shader shader;
        private readonly Texture texture;

        public LogoVisualisation()
        {
            texture = Texture.WhitePixel;
            AccentColour = new Color4(1, 1, 1, 1 / (visualiser_rounds - 1));
            Blending = BlendingMode.Additive;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, IBindableBeatmap beatmap)
        {
            this.beatmap.BindTo(beatmap);
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
        }

        private void shiftIndex()
        {
            var track = beatmap.Value.TrackLoaded ? beatmap.Value.Track : null;

            float[] temporalAmplitudes = track?.CurrentAmplitudes.FrequencyAmplitudes ?? new float[256];

            for (int i = 0; i < bars_per_visualiser; i++)
            {
                if (track?.IsRunning != true)
                {
                    int index = (i + index_change) % bars_per_visualiser;
                    if (frequencyAmplitudes[index] > frequencyAmplitudes[i])
                        frequencyAmplitudes[i] = frequencyAmplitudes[index] - (frequencyAmplitudes[index] / 20);
                }
            }

            indexOffset = (indexOffset + index_change) % bars_per_visualiser;
            Scheduler.AddDelayed(shiftIndex, index_shift_delay);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            shiftIndex();
        }

        protected override void Update()
        {
            base.Update();

            var track = beatmap.Value.TrackLoaded ? beatmap.Value.Track : null;
            var effect = beatmap.Value.BeatmapLoaded ? beatmap.Value.Beatmap.ControlPointInfo.EffectPointAt(track?.CurrentTime ?? Time.Current) : null;

            float[] temporalAmplitudes = track?.CurrentAmplitudes.FrequencyAmplitudes ?? new float[256];

            float decayFactor = (float)Time.Elapsed * decay_per_second / 1000;
            for (int i = 0; i < bars_per_visualiser; i++)
            {
                //3% of extra bar length to make it shrink faster when bar is almost at it's minimum
                frequencyAmplitudes[i] -= decayFactor * (frequencyAmplitudes[i] + 0.03f);
                if (frequencyAmplitudes[i] < 0)
                    frequencyAmplitudes[i] = 0;

                if (track?.IsRunning ?? false)
                {
                    float targetAmplitude = temporalAmplitudes[(i + indexOffset) % bars_per_visualiser] * (effect?.KiaiMode == true ? 1 : 0.5f);
                    if (targetAmplitude > frequencyAmplitudes[i])
                        frequencyAmplitudes[i] = frequencyAmplitudes[i] = targetAmplitude;
                }
            }

            Invalidate(Invalidation.DrawNode, shallPropagate: false);
        }

        protected override DrawNode CreateDrawNode() => new VisualisationDrawNode();

        private readonly VisualiserSharedData sharedData = new VisualiserSharedData();

        protected override void ApplyDrawNode(DrawNode node)
        {
            base.ApplyDrawNode(node);

            var visNode = (VisualisationDrawNode)node;

            visNode.Shader = shader;
            visNode.Texture = texture;
            visNode.Size = DrawSize.X;
            visNode.Shared = sharedData;
            visNode.Colour = AccentColour;
            visNode.AudioData = frequencyAmplitudes;
        }

        private class VisualiserSharedData
        {
            public readonly LinearBatch<TexturedVertex2D> VertexBatch = new LinearBatch<TexturedVertex2D>(1000 * 4, 10, PrimitiveType.Quads);
        }

        private class VisualisationDrawNode : DrawNode
        {
            public Shader Shader;
            public Texture Texture;
            public VisualiserSharedData Shared;
            //Asuming the logo is a circle, we don't need a second dimension.
            public float Size;

            public Color4 Colour;
            public float[] AudioData;

            public override void Draw(Action<TexturedVertex2D> vertexAction)
            {
                base.Draw(vertexAction);

                Shader.Bind();
                Texture.TextureGL.Bind();

                Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                ColourInfo colourInfo = DrawColourInfo.Colour;
                colourInfo.ApplyChild(Colour);

                if (AudioData != null)
                {
                    for (int i = 0; i < bars_per_visualiser; i++)
                    {
                        var localAudioData = new List<float>();
                        for (int j = 0; j < visualiser_rounds; j++)
                        {
                            int dataIndex = (i + (int)(bars_per_visualiser / visualiser_rounds) * j) % bars_per_visualiser;
                            if (AudioData[dataIndex] < amplitude_dead_zone)
                                continue;
                            localAudioData.Add(AudioData[dataIndex]);
                        }

                        if (localAudioData.Count >= (visualiser_rounds == 1 ? 2 : visualiser_rounds))
                            localAudioData.Remove(localAudioData.Min());

                        foreach (var length in localAudioData)
                        {
                            float rotation = MathHelper.DegreesToRadians((float)i / bars_per_visualiser * 360);
                            float rotationCos = (float)Math.Cos(rotation);
                            float rotationSin = (float)Math.Sin(rotation);
                            //taking the cos and sin to the 0..1 range
                            var barPosition = new Vector2(rotationCos / 2 + 0.5f, rotationSin / 2 + 0.5f) * Size;

                            var barSize = new Vector2(bar_length * length, Size * (float)Math.Sqrt(2 * (1 - Math.Cos(MathHelper.DegreesToRadians(360f / bars_per_visualiser)))) / 2);
                            //The distance between the position and the sides of the bar.
                            var bottomOffset = new Vector2(-rotationSin * barSize.Y / 2, rotationCos * barSize.Y / 2);
                            //The distance between the bottom side of the bar and the top side.
                            var amplitudeOffset = new Vector2(rotationCos * barSize.X, rotationSin * barSize.X);

                            var rectangle = new Quad(
                                Vector2Extensions.Transform(barPosition - bottomOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition - bottomOffset + amplitudeOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition + bottomOffset, DrawInfo.Matrix),
                                Vector2Extensions.Transform(barPosition + bottomOffset + amplitudeOffset, DrawInfo.Matrix)
                            );

                            Texture.DrawQuad(
                                rectangle,
                                colourInfo,
                                null,
                                Shared.VertexBatch.AddAction,
                                Vector2.Divide(inflation, barSize));
                        }
                    }
                }
                Shader.Unbind();
            }
        }
    }
}
