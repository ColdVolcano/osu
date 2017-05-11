// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using OpenTK;
using OpenTK.Graphics;
using System;

namespace osu.Game.Screens.Menu
{
    internal class LogoVisualisation : Container
    {
        public override bool HandleInput => false;

        private Bindable<WorkingBeatmap> beatmap;
        private Bindable<bool> kiai = new Bindable<bool>();

        private int indexOffset;
        private double timeOfLastUpdate = double.MinValue;

        private const int bars_per_visualizer = 250;
        private const int number_of_visualizers = 5;
        private const float bar_scale_multiplier = 3.8f;

        public MenuVisualisation()
        {
            Size = new Vector2(460);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            float barWidth = Size.X * (float)Math.Sqrt(2f * (1f - Math.Cos(MathHelper.DegreesToRadians(360f / bars_per_visualizer)))) / 2.2f;

            for (int i = 0; i < number_of_visualizers; i++)
            {
                for (int j = 0; j < bars_per_visualizer; j++)
                {
                    Add(new Box()
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Size = new Vector2(barWidth, 300 * bar_scale_multiplier),
                        RelativePositionAxes = Axes.Both,
                        Scale = new Vector2(1, 0),
                        Colour = Color4.White,
                        Alpha = 0.5f,
                        Position = new Vector2(
                        -(float)Math.Sin((float)(j + i * (bars_per_visualizer / number_of_visualizers)) / bars_per_visualizer * 2 * MathHelper.Pi) / 2,
                        -0.5f + (float)Math.Cos((float)(j + i * (bars_per_visualizer / number_of_visualizers)) / bars_per_visualizer * 2 * MathHelper.Pi) / 2),
                        Rotation = 360f / bars_per_visualizer * j + 180 + 360f / 5 * i,
                    });
                }
            }

            kiai.ValueChanged += updateKiai;
        }

        private void updateKiai(bool newValue)
        {
            FadeTo(newValue ? 1 : 0.4f, 75);
        }

        protected override void Update()
        {
            if (beatmap?.Value != null)
            {
                if ((bool)beatmap.Value?.Track?.IsRunning)
                {
                    ControlPoint kiaiControlPoint;
                    beatmap.Value.Beatmap.TimingInfo.TimingPointAt(beatmap.Value.Track.CurrentTime, out kiaiControlPoint);
                    kiai.Value = (kiaiControlPoint?.KiaiMode ?? false);

                    //Considering 20 checks on the audio data per second
                    if (timeOfLastUpdate + (1000f/20) <= Clock.CurrentTime)
                    {
                        int i = 0;
                        float[] audioData = beatmap.Value.Track.FrequencyAmplitudes;

                        foreach (Box b in Children)
                        {
                            int index = (i % bars_per_visualizer) + indexOffset - index > bars_per_visualizer - 1 ? bars_per_visualizer : 0;

                            if (audioData[index] >= b.Scale.Y)
                            {
                                b.ClearTransforms(true);
                                b.Scale = new Vector2(1, audioData[index]);
                            }
                            i++;
                        }

                        indexOffset += indexOffset == bars_per_visualizer - 5 ? -bars_per_visualizer + 5 : 5;
                        timeOfLastUpdate = Clock.CurrentTime;
                    }
                }
            }
        }

        protected override void UpdateAfterChildren()
        {
            foreach (Box b in Children)
            {
                b.ScaleTo(new Vector2(1, b.Scale.Y  * bar_scale_multiplier * 0.8f), 50);

                if (b.Scale.Y > 0)
                {
                    b.Alpha = MathHelper.Clamp(b.Scale.Y * bar_scale_multiplier - 0.01f, 0, 0.15f) * .2f / 0.15f;
                }
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game)
        {
            beatmap = game.Beatmap;
        }
    }
}
