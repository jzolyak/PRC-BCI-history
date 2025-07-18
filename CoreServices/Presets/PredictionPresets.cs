using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using static CoreServices.Presets.PredictionPresets;

namespace CoreServices.Presets
{
    public class PredictionPresets
    {
        private static PredictionPresets predictionPresets;

        private PresetsService presetsService;

        public static PredictionPresets Instance
        {
            get { if (predictionPresets == null) { predictionPresets = new PredictionPresets(); } return predictionPresets; }
        }

        // Prediction preset categories
        public PredictionCriteriaPresets PredictionCriteriaPresets { get; private set; }
        public DecisionLogicPresets DecisionLogicPresets { get; private set; }
        public AbstentionPresets AbstentionPresets { get; private set; }

        public PredictionPresets()
        {
            presetsService = PresetsService.Instance;

            PredictionCriteriaPresets = new PredictionCriteriaPresets();
            DecisionLogicPresets = new DecisionLogicPresets();
            AbstentionPresets = new AbstentionPresets();

            //PredictionCriteriaPresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "PredictionCriteria"));
            //DecisionLogicPresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "DecisionLogic"));
            //AbstentionPresets.RetreiveSettings(presetsService.GetSettings(Constants.Flash, "Abstention"));
        }
    }

    public class PredictionCriteriaPresets
    {
        public int highestTopScores { get; set; }
        public int winCountForSelection { get; set; }
        public int predictScoreThreshold { get; set; }

        public PredictionCriteriaPresets()
        {
            highestTopScores = 4;                   // Default=4, Range={2, 4}, num of highest scores to search for row/col intersection
            winCountForSelection = 3;               // Default=3, Range={1, maxNumFlashSeq}, num times keyID meets criteria before make prediction
            predictScoreThreshold = 5000;           // Default=5000, Range={3000, 8000}, accum weighted score criteria before make prediction
        }

        //public bool RetreiveSettings(Dictionary<string,string> settings)
        //{
        //}
    }

    public class DecisionLogicPresets
    {
        // Define enumerated types used in Prediction preset configuration.
        public enum ScoringMethod : int { KeyID = 0, StimulusCode = 1 };
        public enum StoppingMethod : int { FixedNumberOfSequences = 0, DynamicStopping = 1 };
        public enum DecisionMethod : int { Forced = 0, Abstention = 1 };

        public ScoringMethod scoringMethod { get; set; }
        public StoppingMethod stoppingMethod { get; set; }
        public DecisionMethod finalDecisionType { get; set; }
        public int maxNumFlashSequences { get; set; }
        public int scoringWindowLength { get; set; }

        public DecisionLogicPresets()
        {
            scoringMethod = ScoringMethod.StimulusCode;         // Default=1, Range={KeyID = 0, StimulusCode = 1} 
            stoppingMethod = StoppingMethod.DynamicStopping;    // Default=1, Range={FixedNumberOfSequences = 0, DynamicStopping = 1} 
            finalDecisionType = DecisionMethod.Abstention;      // Default=1, Range={Forced = 0, Abstention = 1}, what to do if win total not met
            maxNumFlashSequences = 15;                          // Default=15, Range={1, 15}, max flash sequences before final decision is made
            scoringWindowLength = 6;                            // Default=6, Range={1, 15}, num flash seq used in sliding window to computing accum score 
        }

        //public bool RetreiveSettings(Dictionary<string, string> settings)
        //{
        //}
    }

    public class AbstentionPresets
    {
        public int abstentionMessageEnabled { get; set; }
        public int autoPauseAbstentionCount { get; set; }

        public AbstentionPresets()
        {
            abstentionMessageEnabled = 0;           // Default=0, Range={0, 1}, Enable popup message (=1) to appear when no prediction is detected
            autoPauseAbstentionCount = 5;           // Default=5, Range={5, 15},Number of consecutive abstentions before switch to AutoPause mode
        }

        /*
        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
        }
        */
    }

}
