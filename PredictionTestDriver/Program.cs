using System;
using System.IO;

using CoreServices.Prediction;
using CoreServices.Presets;
using CoreServices.SensorData;
using static CoreServices.Prediction.PredictionTestEngine;

class Program
{
    static async Task Main(string[] args)
    {

        // NOTE: Uncomment these 2 lines to run this test.
        // BinaryDataTestEngine testEngine = new BinaryDataTestEngine();
        // await testEngine.BinaryDataTestMode();

        /* ************************************************************ */

        PredictionTestEngine testEngine = new PredictionTestEngine();

        TestInputFileInfo inputFileInfo = new TestInputFileInfo();

        // Start with preset configuration default values built into code instantiation.
        PredictionPresets predictionConfiguration = PredictionPresets.Instance;

        string testInputDataFilename;
        string testInputParamFilename;

        int errorCode = 0;

        if (args.Length == 0)
        {
            Console.WriteLine("Error - No command line arguments were provided for offline testing\n");
            return;
        }
        else if (args.Length == 1)
        {
            // User didn't specify Prediction preset parameters. Use default values as test input.
            testInputDataFilename = args[0];
            testInputParamFilename = "OfflinePredictionParameters.txt";
        }
        else
        {
            testInputDataFilename = args[0];
            testInputParamFilename = args[1];
        }

        // Get the base file name, and calibration start/end/total num of sets information,
        // as well as weight coef file name and acuracy report file name.
        errorCode = testEngine.ParseTestInputFileInfo(testInputDataFilename, ref inputFileInfo);

        if (errorCode != 0)
        {
            Console.WriteLine("Error - Could not access test input info file:" + testInputDataFilename + "\n");
            return;
        }

        // Second Main argument is text file that can be used to override all, or any individual
        // Prediction presets parameters that are specified in this file.
        errorCode = testEngine.ParserOfflinePredictionParameters(testInputParamFilename, ref predictionConfiguration);

        if (errorCode != 0)
        {
            Console.WriteLine("Error - Could not access prediction parameter file:" + testInputParamFilename + "\n");
            return;
        }

        //await testEngine.PredictionTestMode();
        await testEngine.PredictionTestMode(inputFileInfo, predictionConfiguration);

        Console.WriteLine("Processing finished. Press ENTER key to close window ... \n");
        Console.ReadLine();
    }

    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

}
