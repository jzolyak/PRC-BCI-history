using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

using CoreServices.Prediction;

namespace CoreServices.Prediction
{
    // Define enumerated types used in Prediction preset configuration.
    public enum PredictionMethod : int { KeyID = 0, StimulusCode = 1 };
    public enum StoppingMethod : int { FixedNumberOfSequences = 0, DynamicStopping = 1 };

    public struct PredictionPresets
    {
        // Two algorithms are available to predict which keyID the user is focused on.
        // KeyID algorithm is based on the accumulated weighted scoring of keyIDs over time.
        // StimulusCode algorithm is based on the accumulated weighted scoring of stimulus
        // code intersecting point over time.
        public PredictionMethod predictionMethod;   // Default = 1

        // Two methods are available for deciding when to stop collecting sensor data
        // and make a final selection on which keyID the user is focused on.
        public StoppingMethod stoppingMethod;       // Default = 0

        // Minimum number of times a keyID must be detected as winner of flash sequence 
        // before make final selection for dynamic stopping prediction method.
        public int winCountForSelection;        // Default = 3

        // Maximum number of times the flash sequence data is collected before stopping
        // and making a keyID selection.
        public int fixedSequenceLimit;          // Default = 15

        // Accumulated score must be greater than this level to be considered 
        // real evoked signal and not just noise.
        public int predictScoreThreshold;       // Default = 5000


        // Until figure out how to transfer the preset configuration values from
        // a file at BCI startup, temporarily assign presets a default value here...

        public PredictionPresets()
        {
            predictionMethod = PredictionMethod.StimulusCode;
            stoppingMethod   = StoppingMethod.FixedNumberOfSequences;
            winCountForSelection  = 3;
            fixedSequenceLimit    = 15;
            //fixedSequenceLimit    = 10;
            predictScoreThreshold = 5000;
        }
    }
}
