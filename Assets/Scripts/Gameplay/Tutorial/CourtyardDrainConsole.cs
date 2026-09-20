using CoastalTemple.Interaction;
using CoastalTemple.Mechanisms;
using UnityEngine;
namespace CoastalTemple.Tutorial
{
    // Preserve the original pool experiment through a nearby, visible interaction.
    public sealed class CourtyardDrainConsole : TutorialInteractable
    {
        public CourtyardDrainExperiment experiment;
        [HideInInspector] public CoastalWalkthrough walkthrough;
        public override bool Available => base.Available && experiment;
        public override string DisplayPrompt => experiment && experiment.Powered
            ? "重新蓄水，复原浮台" : "开启折射光源，排空水池";

        public override void Use(PlayerInteractor actor)
        {
            if (!Available) return;
            experiment.Toggle();
        }
    }
}

