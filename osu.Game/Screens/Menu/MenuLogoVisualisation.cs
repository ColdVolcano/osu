// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;
using osu.Game.Skinning;
using osu.Game.Online.API;
using osu.Game.Users;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using Intro = osu.Game.Configuration.IntroSequence;

namespace osu.Game.Screens.Menu
{
    internal class MenuLogoVisualisation : CompositeDrawable
    {
        private Bindable<User> user;
        private Bindable<Skin> skin;
        private Bindable<Intro> playedIntro;
        private Bindable<Intro> selectedIntro;
        private Intro? appliedStyle;
        private LogoVisualisation primaryVisualisation;
        private LogoVisualisation secondaryVisualisation;

        [Resolved]
        private TextureStore textures { get; set; }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api, SkinManager skinManager, OsuConfigManager config, SessionStatics sessionStatics)
        {
            AddRangeInternal(new[]
            {
                primaryVisualisation = new LogoVisualisation
                {
                    RelativeSizeAxes = Axes.Both,
                },
                secondaryVisualisation = new LogoVisualisation
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });

            user = api.LocalUser.GetBoundCopy();
            skin = skinManager.CurrentSkin.GetBoundCopy();
            playedIntro = sessionStatics.GetBindable<Intro>(Static.IntroSequencePlayed);
            selectedIntro = config.GetBindable<Intro>(OsuSetting.IntroSequence);

            user.ValueChanged += _ => updateColour();
            skin.BindValueChanged(_ => updateColour(), true);
            playedIntro.ValueChanged += _ => updateStyles();
            selectedIntro.BindValueChanged(_ => updateStyles(), true);
        }

        private void updateColour()
        {
            if (user.Value?.IsSupporter ?? false)
                Colour = skin.Value.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? Color4.White;
            else
                Colour = Color4.White;
        }

        private void updateStyles()
        {
            Intro targetStyle = selectedIntro.Value == Intro.Random ? playedIntro.Value : selectedIntro.Value;

            if (targetStyle == appliedStyle)
                return;

            switch (targetStyle)
            {
                case Intro.Circles:
                    appliedStyle = Intro.Circles;
                    primaryVisualisation.ApplyCirclesStyle();
                    secondaryVisualisation.Magnitude = 0;
                    break;

                case Intro.Welcome:
                    appliedStyle = Intro.Welcome;
                    primaryVisualisation.ApplyWelcomeStyle();
                    secondaryVisualisation.Magnitude = 0;
                    break;

                case Intro.Triangles:
                    appliedStyle = Intro.Triangles;
                    primaryVisualisation.ApplyTrianglesStyle();
                    primaryVisualisation.Texture = textures.Get(@"Menu/visualiser-bar-shallow");
                    secondaryVisualisation.ApplyTiltedTrianglesStyle();
                    secondaryVisualisation.Texture = textures.Get(@"Menu/visualiser-bar");
                    break;
            }
        }
    }
}
