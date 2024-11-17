using System;
using System.Collections.Generic;
using System.Text;
using TSBase;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elin_SkillHelper
{
    public class SkillHelperWindow : CustomUI<SkillHelperWindow.Args>
    {
        public struct Args : ICustomUIArgs
        {

        }

        public override CustomUI<Args> Setup(Args args)
        {
            base.Setup(args);

            var window = this.AddWindow(400, 400, "test");
            var layout = window.MakeLayout();

            layout.AddText("Hello");
            layout.AddText("World!");

            var sublayout = layout.Horizontal(100);
            sublayout.AddText("Horizontally");
            sublayout.AddText("alinged");
            sublayout.AddToggle("Toggles!", toggled => Debug.Log($"Toggled: {toggled}"));

            window.layer.option.screenClickCloseRight = false;

            return this;
        }

    }
}
