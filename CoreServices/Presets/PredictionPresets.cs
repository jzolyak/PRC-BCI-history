using CoreServices.Common;
using CoreServices.Models;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;

namespace CoreServices.Presets
{
    public class PredictionPresets: BasePresets
    {
        private static PredictionPresets predictionPresets;

        public static PredictionPresets Instance
        {
            get
            {
                if (predictionPresets == null)
                {
                    predictionPresets = new PredictionPresets();
                }
                return predictionPresets;
            }
        }

        // Prediction preset categories
        public PredictionCriteriaPresets PredictionCriteriaPresets { get; private set; }
        public DecisionLogicPresets DecisionLogicPresets { get; private set; }
        public AbstentionPresets AbstentionPresets { get; private set; }

        private PredictionPresets()
        {
            PredictionCriteriaPresets = new PredictionCriteriaPresets();
            DecisionLogicPresets = new DecisionLogicPresets();
            AbstentionPresets = new AbstentionPresets();

            // Load initial settings
            LoadPresets();
        }

        protected override void LoadPresets()
        {
            PredictionCriteriaPresets.RetreiveSettings(
                presetsService.GetSettings(Constants.Prediction, "PredictionCriteria"));
            DecisionLogicPresets.RetreiveSettings(
                presetsService.GetSettings(Constants.Prediction, "DecisionLogic"));
            AbstentionPresets.RetreiveSettings(
                presetsService.GetSettings(Constants.Prediction, "Abstention"));
        }
    }

    public class PredictionCriteriaPresets
    {
        public int highestTopScores { get; set; }
        public int winCountForSelection { get; set; }
        public int predictScoreThreshold { get; set; }

        public PredictionCriteriaPresets()
        {
            highestTopScores = 4;
            winCountForSelection = 3;
            predictScoreThreshold = 5000;
        }

        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if (settings == null) return false;

            bool updated = false;

            foreach (var kvp in settings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

                switch (kvp.Key.ToLowerInvariant())
                {
                    case "user;highesttopscores":
                        if (int.TryParse(kvp.Value, out int topScores))
                        {
                            highestTopScores = topScores;
                            updated = true;
                        }
                        else highestTopScores = 4;
                        break;

                    case "user;wincountthreshold":
                        if (int.TryParse(kvp.Value, out int winCount))
                        {
                            winCountForSelection = winCount;
                            updated = true;
                        }
                        else winCountForSelection = 3;
                        break;

                    case "user;predictscorethreold":
                        if (int.TryParse(kvp.Value, out int threshold))
                        {
                            predictScoreThreshold = threshold;
                            updated = true;
                        }
                        else predictScoreThreshold = 5000;
                        break;
                }
            }

            return updated;
        }
    }

    public class DecisionLogicPresets
    {
        public enum ScoringMethod : int { KeyID = 0, StimulusCode = 1 }
        public enum StoppingMethod : int { FixedNumberOfSequences = 0, DynamicStopping = 1 }
        public enum DecisionMethod : int { Forced = 0, Abstention = 1 }

        public ScoringMethod scoringMethod { get; set; }
        public StoppingMethod stoppingMethod { get; set; }
        public DecisionMethod finalDecisionType { get; set; }
        public int maxNumFlashSequences { get; set; }
        public int scoringWindowLength { get; set; }

        public DecisionLogicPresets()
        {
            scoringMethod = ScoringMethod.StimulusCode;
            stoppingMethod = StoppingMethod.DynamicStopping;
            finalDecisionType = DecisionMethod.Abstention;
            maxNumFlashSequences = 15;
            scoringWindowLength = 6;
        }

        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if (settings == null) return false;

            bool updated = false;

            foreach (var kvp in settings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

                switch (kvp.Key.ToLowerInvariant())
                {
                    case "user;scoringmethod":
                        if (Enum.TryParse(kvp.Value, true, out ScoringMethod sm))
                        {
                            scoringMethod = sm; updated = true;
                        }
                        else scoringMethod = ScoringMethod.StimulusCode;
                        break;

                    case "user;stoppingmethod":
                        if (Enum.TryParse(kvp.Value, true, out StoppingMethod stop))
                        {
                            stoppingMethod = stop; updated = true;
                        }
                        else stoppingMethod = StoppingMethod.DynamicStopping;
                        break;

                    case "user;finaldecisiontype":
                        if (Enum.TryParse(kvp.Value, true, out DecisionMethod dm))
                        {
                            finalDecisionType = dm; updated = true;
                        }
                        else finalDecisionType = DecisionMethod.Abstention;
                        break;

                    case "user;maxnumflashsequences":
                        if (int.TryParse(kvp.Value, out int maxSeq))
                        {
                            maxNumFlashSequences = maxSeq; updated = true;
                        }
                        else maxNumFlashSequences = 15;
                        break;

                    case "user;scoringwindowlength":
                        if (int.TryParse(kvp.Value, out int winLen))
                        {
                            scoringWindowLength = winLen; updated = true;
                        }
                        else scoringWindowLength = 6;
                        break;
                }
            }

            return updated;
        }
    }

    public class AbstentionPresets
    {
        public int AbstentionMessageEnabled { get; set; }
        public int AutoPauseAbstentionCount { get; set; }

        public AbstentionPresets()
        {
            AbstentionMessageEnabled = 0;
            AutoPauseAbstentionCount = 5;
        }

        public bool RetreiveSettings(Dictionary<string, string> settings)
        {
            if (settings == null) return false;

            bool updated = false;

            foreach (var kvp in settings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

                switch (kvp.Key.ToLowerInvariant())
                {
                    case "user;abstentionmessageenabled":
                        if (int.TryParse(kvp.Value, out int messageEnabled))
                        {
                            AbstentionMessageEnabled = messageEnabled; updated = true;
                        }
                        else AbstentionMessageEnabled = 0;
                        break;

                    case "user;autopauseabstentioncount":
                        if (int.TryParse(kvp.Value, out int pauseCount))
                        {
                            AutoPauseAbstentionCount = pauseCount; updated = true;
                        }
                        else AutoPauseAbstentionCount = 5;
                        break;
                }
            }

            return updated;
        }
    }
}
