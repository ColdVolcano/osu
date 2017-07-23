using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Game.Graphics;
using osu.Game.Screens.Menu;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;

namespace osu.Desktop.VisualTests.Tests
{
    public class TestCaseIntro : TestCase
    {
        Intro intro;
        public TestCaseIntro()
        {
            Add(new Box
            {
                RelativeSizeAxes = Framework.Graphics.Axes.Both,
                Colour = OsuColour.FromHex("000000"),
            });
            AddStep("Play Intro", () => Add(new Intro(true)));
        }
    }
}
