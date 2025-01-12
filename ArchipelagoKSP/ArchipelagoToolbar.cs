using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ToolbarControl_NS;

namespace ArchipelagoKSP
{

    public class ArchipelagoToolbar
    {
        [KSPAddon(KSPAddon.Startup.MainMenu, true)]
        public class RegisterToolbar : MonoBehaviour
        {
            public void Start()
            {
                ToolbarControl.RegisterMod(ArchipelagoManager.MODID, ArchipelagoManager.MODNAME);
                DontDestroyOnLoad(this);
            }
        }
    }
}
