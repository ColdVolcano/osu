// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.MathUtils;
using osu.Framework.Screens;
using osu.Game.Beatmaps.IO;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Backgrounds;

namespace osu.Game.Screens.Menu
{
    public class Intro : OsuScreen
    {
        private readonly ParallaxContainer parallaxContainer;
        private readonly CircularContainer lightBlueishCircle; //ScaleTo(1, 750, someEasing)
        private readonly CircularContainer biggerBlackCircle;
        private readonly CircularContainer semiOpaqueCircle; //150 ms wait, then Scale to 0.3 for 250, then Scale to 1 for 50, then scale to 0.95 for 33, then scale to 1 for 467, then fade out
        private readonly CircularContainer blackCircle;
        private readonly CircularContainer whiteCircle;
        private readonly CircularContainer blackLittleCircle;
        private readonly Container linesContainer;
        private readonly OsuSpriteText text;
        private readonly OsuLogo logo;

        public const string MENU_MUSIC_BEATMAP_HASH = "21c1271b91234385978b5418881fdd88";

        /// <summary>
        /// Whether we have loaded the menu previously.
        /// </summary>
        internal bool DidLoadMenu;

        private MainMenu mainMenu;
        private SampleChannel welcome;
        private SampleChannel seeya;

        internal override bool HasLocalCursorDisplayed => true;

        internal override bool ShowOverlays => false;

        bool isVisualTest;

        protected override BackgroundScreen CreateBackground() => new BackgroundScreenEmpty();

        public Intro(bool visualTest = false)
        {
            isVisualTest = visualTest;
            Children = new Drawable[]
            {
                parallaxContainer = new ParallaxContainer
                {
                    ParallaxAmount = 0.01f,
                    Children = new Drawable[]
                    {
                        lightBlueishCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(400),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = OsuColour.FromHex("AEBADB"),
                                Alpha = 1,
                            }
                        },
                        biggerBlackCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(401f),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 1,
                            }
                        },
                        semiOpaqueCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(186),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Alpha = 0.5f,
                            }
                        },
                        blackCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(187),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 1,
                            }
                        },
                        whiteCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(68.5f),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Alpha = 1,
                            }
                        },
                        blackLittleCircle = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Size = new Vector2(69.5f),
                            Scale = Vector2.Zero,
                            Child = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 1,
                            }
                        },
                        linesContainer = new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(400),
                            Alpha = 0,
                        },
                        text = new OsuSpriteText
                        {
                            Text = "welcome",
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            TextSize = 50,
                            Colour = Color4.White,
                            Alpha = 0,
                            Font = "Exo2.0-Regular"
                        },
                        logo = new OsuLogo
                        {
                            Depth = -6,
                            Alpha = 0,
                            Interactive = false,
                            Ripple = false,
                        }
                    }
                }
            };
            for (int i = 0; i < 4; i++)
            {
                linesContainer.Add(new Box
                {
                    Anchor = ((i <= 1 ? Anchor.x2 : Anchor.x0) | (i % 3 == 0 ? Anchor.y0 : Anchor.y2)),
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(180, 3),
                    Rotation = 135 + i * 90,
                    Colour = Color4.White,
                    Alpha = 0.2f * (i + 1),
                    Position = new Vector2((i <= 1 ? -1 : 1), (i % 3 == 0 ? 1 : -1)) * linesContainer.Size / 2,
                    Scale = new Vector2(0, 1),
                });
            }
        }

        private Bindable<bool> menuVoice;
        private Bindable<bool> menuMusic;
        private Track track;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config, BeatmapDatabase beatmaps, Framework.Game game)
        {
            menuVoice = config.GetBindable<bool>(OsuSetting.MenuVoice);
            menuMusic = config.GetBindable<bool>(OsuSetting.MenuMusic);

            BeatmapSetInfo setInfo = null;

            if (!menuMusic)
            {
                var query = beatmaps.Query<BeatmapSetInfo>().Where(b => !b.DeletePending);
                int count = query.Count();
                if (count > 0)
                    setInfo = query.ElementAt(RNG.Next(0, count - 1));
            }

            if (setInfo == null)
            {
                var query = beatmaps.Query<BeatmapSetInfo>().Where(b => b.Hash == MENU_MUSIC_BEATMAP_HASH);

                setInfo = query.FirstOrDefault();

                if (setInfo == null)
                {
                    // we need to import the default menu background beatmap
                    beatmaps.Import(new OszArchiveReader(game.Resources.GetStream(@"Tracks/circles.osz")));

                    setInfo = query.First();

                    setInfo.DeletePending = true;
                    beatmaps.Update(setInfo, false);
                }
            }

            beatmaps.GetChildren(setInfo);
            Beatmap.Value = beatmaps.GetWorkingBeatmap(setInfo.Beatmaps[0]);

            track = Beatmap.Value.Track;

            welcome = audio.Sample.Get(@"welcome");
            seeya = audio.Sample.Get(@"seeya");
        }

        protected override void OnEntering(Screen last)
        {
            base.OnEntering(last);

            if (menuVoice)
                welcome.Play();

            Scheduler.AddDelayed(delegate
            {
                if (!isVisualTest)
                {
                    // Only start the current track if it is the menu music. A beatmap's track is started when entering the Main Manu.
                    if (menuMusic)
                        track.Start();
                    LoadComponentAsync(mainMenu = new MainMenu());
                }
                Scheduler.AddDelayed(delegate
                {
                    if (isVisualTest)
                        logo.FadeOut(300);
                    else
                    {
                        DidLoadMenu = true;
                        Push(mainMenu);
                    }
                }, 2300);
            }, 600);

            lightBlueishCircle.ScaleTo(0.75f, 250, Easing.InCubic)
                .Then()
                .ScaleTo(1, 450, Easing.OutCubic)
                .Then()
                .FadeOut();
            Scheduler.AddDelayed(delegate
            {
                parallaxContainer.ChangeChildDepth(lightBlueishCircle, -4);
                lightBlueishCircle.Origin = Anchor.CentreLeft;
                lightBlueishCircle.Child.Colour = OsuColour.FromHex("8CD6EC");
            }, 1350);
            using (lightBlueishCircle.BeginDelayedSequence(1450))
            {
                lightBlueishCircle.MoveTo(new Vector2(-200, 0))
                    .RotateTo(180)
                    .ResizeTo(logo.Size.X * 0.865f)
                    .ScaleTo(0)
                    .FadeIn()
                    .RotateTo(0, 600, Easing.InOutCubic)
                    .ScaleTo(0.75f, 250, Easing.InCubic)
                    .Delay(150)
                    .MoveTo(new Vector2(logo.Size.X * 0.865f / -2, 0), 300)
                    .Delay(100)
                    .ScaleTo(1, 500, Easing.OutCubic)
                    .Then()
                    .Delay(200)
                    .FadeOut();
            }
            using (biggerBlackCircle.BeginDelayedSequence(100))
            {
                biggerBlackCircle.ScaleTo(0.2f, 150, Easing.InCubic)
                    .Then()
                    .ScaleTo(1, 450, Easing.OutCubic)
                    .Then()
                    .FadeOut();
            }
            Scheduler.AddDelayed(delegate
            {
                parallaxContainer.ChangeChildDepth(biggerBlackCircle, -3);
                biggerBlackCircle.Origin = Anchor.TopCentre;
                biggerBlackCircle.Child.Colour = OsuColour.FromHex("EFC953");
            }, 1350);
            using (biggerBlackCircle.BeginDelayedSequence(1400))
            {
                biggerBlackCircle.MoveTo(new Vector2(0, -200))
                    .RotateTo(180)
                    .ResizeTo(logo.Size.X * 0.865f)
                    .ScaleTo(0)
                    .FadeIn()
                    .RotateTo(0, 600, Easing.InOutQuad)
                    .ScaleTo(0.75f, 250, Easing.InCubic)
                    .Delay(150)
                    .MoveTo(new Vector2(0, logo.Size.X * 0.865f / -2), 300)
                    .Delay(100)
                    .ScaleTo(1, 550, Easing.OutCubic)
                    .Then()
                    .Delay(600)
                    .FadeOut();
            }
            using (semiOpaqueCircle.BeginDelayedSequence(150))
            {
                semiOpaqueCircle.ScaleTo(0.3f, 250, Easing.InCubic)
                    .Then()
                    .ScaleTo(1, 50)
                    .Then()
                    .ScaleTo(0.9f, 33, Easing.OutCubic)
                    .Then()
                    .ScaleTo(1, 467)
                    .Then()
                    .FadeOut();
            }
            Scheduler.AddDelayed(() => parallaxContainer.ChangeChildDepth(semiOpaqueCircle, 0), 1350);
            using (semiOpaqueCircle.BeginDelayedSequence(1350, true))
            {
                semiOpaqueCircle.ResizeTo(logo.Size * 0.965f)
                    .FadeIn();
                (semiOpaqueCircle.Child as Box)
                    .ResizeHeightTo(0)
                    .Then()
                    .ResizeHeightTo(1, 350, Easing.InOutCirc)
                    .RotateTo(-90, 350, Easing.InCubic)
                    .Then()
                    .Delay(600)
                    .FadeOut();
            }
            using (blackCircle.BeginDelayedSequence(200))
            {
                blackCircle.ScaleTo(0.18f, 200, Easing.InCubic)
                    .Then()
                    .ScaleTo(0.66f, 50)
                    .Then()
                    .ScaleTo(0.82f, 33, Easing.OutCubic)
                    .Then()
                    .ScaleTo(1, 467)
                    .Then()
                    .FadeOut();
            }
            Scheduler.AddDelayed(delegate
            {
                parallaxContainer.ChangeChildDepth(blackCircle, -2);
                blackCircle.Origin = Anchor.BottomCentre;
                blackCircle.Child.Colour = OsuColour.FromHex("A28EEE");
            }, 1350);
            using (blackCircle.BeginDelayedSequence(1350))
            {
                blackCircle.MoveTo(new Vector2(0, 200))
                    .RotateTo(180)
                    .ResizeTo(logo.Size.X * 0.865f)
                    .ScaleTo(0)
                    .FadeIn()
                    .RotateTo(0, 600, Easing.InOutQuad)
                    .ScaleTo(0.75f, 250, Easing.InCubic)
                    .Delay(150)
                    .MoveTo(new Vector2(0, logo.Size.X * 0.865f / 2), 300)
                    .Delay(100)
                    .ScaleTo(1, 600, Easing.OutCubic)
                    .Then()
                    .Delay(600)
                    .FadeOut();
            }
            using (whiteCircle.BeginDelayedSequence(150))
            {
                whiteCircle.ScaleTo(0.42f, 250, Easing.InCubic)
                    .Then()
                    .ScaleTo(0.91f, 33)
                    .Then()
                    .ScaleTo(1, 33)
                    .Then()
                    .Delay(484)
                    .FadeOut();
            }
            Scheduler.AddDelayed(() => parallaxContainer.ChangeChildDepth(whiteCircle, -1), 1385);
            using (whiteCircle.BeginDelayedSequence(1385, true))
            {
                whiteCircle.ResizeTo(logo.Size * 0.965f)
                    .FadeIn();
                (whiteCircle.Child as Box).ResizeWidthTo(0)
                    .Then()
                    .ResizeWidthTo(1, 350, Easing.InOutCirc)
                    .RotateTo(-90, 350, Easing.InCubic)
                    .Then()
                    .Delay(800)
                    .FadeOut();
            }
            using (blackLittleCircle.BeginDelayedSequence(150))
            {
                blackLittleCircle.ScaleTo(0.16f, 250, Easing.InCubic)
                    .Then()
                    .ScaleTo(0.58f, 33)
                    .Then()
                    .ScaleTo(0.79f, 67)
                    .Then()
                    .ScaleTo(1, 500)
                    .Then()
                    .FadeOut();
            }
            Scheduler.AddDelayed(delegate
            {
                parallaxContainer.ChangeChildDepth(blackLittleCircle, -5);
                blackLittleCircle.Origin = Anchor.CentreRight;
                blackLittleCircle.Child.Colour = logo.OsuPink;
            }, 1500);
            using (blackLittleCircle.BeginDelayedSequence(1500))
            {
                blackLittleCircle.MoveTo(new Vector2(200, 0))
                    .RotateTo(180)
                    .ResizeTo(logo.Size.X * 0.865f)
                    .ScaleTo(0)
                    .FadeIn()
                    .RotateTo(0, 600, Easing.InOutQuad)
                    .ScaleTo(0.75f, 200, Easing.InCubic)
                    .Delay(150)
                    .MoveTo(new Vector2(logo.Size.X * 0.865f / 2, 0), 300)
                    .Delay(50)
                    .ScaleTo(1, 450, Easing.OutCubic)
                    .Then()
                    .Delay(600)
                    .FadeOut();
            }
            Scheduler.AddDelayed(() => parallaxContainer.ChangeChildDepth(text, 1), 1350);
            using (text.BeginDelayedSequence(400))
            {
                text.TransformSpacingTo(new Vector2(-20, 1))
                    .Then()
                    .TransformSpacingTo(new Vector2(15, 1), 1350, Easing.OutCirc)
                    .FadeIn(75)
                    .Then()
                    .Delay(500)
                    .FadeOut();
            }
            int i = 0;
            foreach (Box b in linesContainer.Children)
            {
                using (b.BeginDelayedSequence(250))
                {
                    b.MoveTo(new Vector2((i <= 1 ? -1 : 1) * 20, (i++ % 3 == 0 ? 1 : -1) * 20), 200, Easing.InCirc)
                        .Then()
                        .MoveTo(Vector2.Zero, 800, Easing.OutCirc);
                    b.ScaleTo(0.95f, 200, Easing.InCirc)
                        .Then()
                        .ScaleTo(1, 50)
                        .Delay(50)
                        .Then()
                        .ScaleTo(new Vector2(0.2f, 1), 200, Easing.InOutCirc)
                        .Then()
                        .ScaleTo(new Vector2(0, 1), 450)
                        .Then()
                        .FadeOut();
                }
            }
            linesContainer.Delay(250)
                .FadeIn()
                .Delay(900)
                .FadeOut();
            using (logo.BeginDelayedSequence(2200, true))
                logo.FadeIn(200);
        }

        protected override void OnSuspending(Screen next)
        {
            Content.FadeOut(300);
            base.OnSuspending(next);
        }

        protected override bool OnExiting(Screen next)
        {
            //cancel exiting if we haven't loaded the menu yet.
            return !DidLoadMenu;
        }

        protected override void OnResuming(Screen last)
        {
            logo.Triangles = false;
            logo.Alpha = 0.4f;
            if (!(last is MainMenu))
                Content.FadeIn(300);

            double fadeOutTime = 2000;
            //we also handle the exit transition.
            if (menuVoice)
                seeya.Play();
            else
                fadeOutTime = 500;

            Scheduler.AddDelayed(Exit, fadeOutTime);

            Game.AlwaysPresent = true;
            Game.FadeOut(fadeOutTime);

            base.OnResuming(last);
        }
    }
}
