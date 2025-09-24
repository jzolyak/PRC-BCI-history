using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics;

using CoreServices.SensorData;

namespace CoreServices.SensorData
{

    // Define what each phase represents
    public enum StimPhase : Byte { NoStim = 0, PreStim = 1, ActiveStim = 2, PostStim = 3, Halted = 4 };

    // Events occur when stimulus markers for phase (state) or stim code change values.
    public enum EventType : int { NoEvent = 0, StateChange = 1, StimCodeChange = 2, ErrorEvent = 3 };

    // Actions that occur at either the beginning or end of the detected event.
    public enum ActionType : int
    {
        NoAction = 0, MarkStateChange = 1, ComputeStats = 2, AllocateScoringTables = 3, ComputeScore = 4,
        StimulusEnd = 5, ReportStateError = 6, ReportStimError = 7
    };

    // Sometimes an event will trigger an immediate action to be taken, while other times
    // need to wait until enough data is buffered first. Define a structure to contain the
    // necessary parameters to perfom an action when its taken from the queue.

    public struct EventAction
    {
        public EventType eventType;
        public ActionType actionType;
        public int eventStartHeaderIndex;
        public int windowLength;
    }

    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    public class StimulusEvent
    {
        // NOTE: PRESET_WINDOW_LENGTH is a *CRITICAL* preset configuration value.
        //       Need to find a way to pass it to PredictionStateEngine, as well as
        //       all the library code that need it.
        //
        // For now create constant value to use in its place.
        public const int PRESET_WINDOW_LENGTH = 240;    // Number of sample intervals that specify length of
                                                        // processing windows, as well as length of weight
                                                        // coefficient vector.

        public StimPhase currentState = StimPhase.Halted;
        public StimPhase nextState = StimPhase.Halted;

        public bool allocateScoringTables = false;

        public Byte currentStimCode = 0;
        public Byte nextStimCode = 0;
        public Byte numStimulusGroups = 0;

        const int EVENT_QUEUE_LENGTH = 16;

        Queue<EventAction> eventQueue = new Queue<EventAction>(EVENT_QUEUE_LENGTH);


        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public EventAction TrackStimulusMarkers(PacketizedSensorData sensorData)
        {
            EventAction eventAction = new EventAction();
            EventAction queuedEvent = new EventAction();

            bool eventFound = false;

            int startIndex = 0;
            int firstSampleIndex = 0;
            int lastSampleIndex = 0;
            int numBufferedSamples = 0;

            // Get current location of read and write indexes where packet headers are stored.
            int readIndex = sensorData.headerTableReadIndex;
            int writeIndex = sensorData.headerTableWriteIndex;


            // Search for change in phase (state) or stim code values in recently received
            // block of packetized sensor data.
            while ((readIndex != writeIndex) && (eventFound == false))
            {
                // The stimulus cycles thru four distinct phases (states) for each predicted key selection.
                // What action is taken depends on which state you are in, and whether or not the grouping
                // of keys that is flashed has changed.
                nextState = (StimPhase)sensorData.packetHeaderTable[readIndex].phaseInSeq;

                switch (currentState)
                {
                    case StimPhase.NoStim:
                        if (nextState == currentState)
                        {  // No change in state. Examine next packet header
                            readIndex = (++readIndex) & (PacketizedSensorData.HEADER_CIRC_BUF_LEN - 1);
                        }
                        else if (nextState == StimPhase.PreStim)
                        {  // Next valid state found
                            currentState = nextState;
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // When state changes to PreStim, two events have actually occurred.
                            // First, put start of PreStim onto queue to determine state length later on.
                            queuedEvent.eventType = EventType.StateChange;
                            queuedEvent.actionType = ActionType.ComputeStats;
                            queuedEvent.eventStartHeaderIndex = readIndex;
                            queuedEvent.windowLength = 0;       // Fill in value later
                            eventQueue.Enqueue(queuedEvent);

                            // Second, return back event marking the beginning of PreStim state.
                            eventAction.eventType = EventType.StateChange;
                            eventAction.actionType = ActionType.MarkStateChange;
                            eventAction.eventStartHeaderIndex = readIndex;
                            eventAction.windowLength = 0;       // No use for NoStim data
                        }
                        else
                        {  // Invalid state change found
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // Error occurred between when packet header is created and reading it now.
                            eventAction.eventType = EventType.ErrorEvent;
                            eventAction.actionType = ActionType.ReportStateError;
                            eventAction.eventStartHeaderIndex = readIndex;
                            eventAction.windowLength = 0;
                        }
                        break;

                    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    case StimPhase.PreStim:

                        if (nextState == currentState)
                        {  // No change in state. Examine next packet header
                            readIndex = (++readIndex) & (PacketizedSensorData.HEADER_CIRC_BUF_LEN - 1);
                        }
                        else if (nextState == StimPhase.ActiveStim)
                        {
                            if (allocateScoringTables == false)
                            {
                                // Don't change state yet. Need to send back two queued messages
                                // before we can change state to ActiveStim.
                                currentStimCode = sensorData.packetHeaderTable[readIndex].stimulusCode;
                                lastSampleIndex = sensorData.packetHeaderTable[readIndex].crossRefIndex;

                                eventFound = true;
                                allocateScoringTables = true;

                                // Save location where we stopped search
                                sensorData.headerTableReadIndex = readIndex;

                                // When state changes to ActiveStim, two events have actually occurred.
                                // First, marker onto queue to indicate start of ActiveStim and now know the
                                // number of stimulus groupings in next target selection to allocate scoring tables.
                                queuedEvent.eventType = EventType.StateChange;
                                queuedEvent.actionType = ActionType.AllocateScoringTables;
                                queuedEvent.eventStartHeaderIndex = readIndex;
                                queuedEvent.windowLength = 0;       // Fill in value later
                                eventQueue.Enqueue(queuedEvent);    // Store at back of queue

                                // Next return back event marking the end of PreStim state.
                                eventAction = eventQueue.Dequeue(); // Read from front of queue

                                startIndex = eventAction.eventStartHeaderIndex;
                                firstSampleIndex = sensorData.packetHeaderTable[startIndex].crossRefIndex;

                                // Determine length of PreStim state based on number of samples collected
                                eventAction.windowLength = lastSampleIndex - firstSampleIndex;
                            }
                            else
                            {
                                // Complete second action when find start of ActiveStim
                                // Now mark next valid state found
                                currentState = nextState;
                                currentStimCode = sensorData.packetHeaderTable[readIndex].stimulusCode;

                                eventFound = true;

                                // Save location where we stopped search
                                sensorData.headerTableReadIndex = readIndex;

                                // Now put start of ActiveStim onto queue to determine flash interval length.
                                queuedEvent.eventType = EventType.StimCodeChange;
                                queuedEvent.actionType = ActionType.ComputeScore;
                                queuedEvent.eventStartHeaderIndex = readIndex;
                                queuedEvent.windowLength = 0;       // Fill in value later
                                eventQueue.Enqueue(queuedEvent);    // Store at back of queue

                                // Finally, return back event marking the begin of ActiveStim state
                                // and perform allocation of memory for scoring tables.
                                eventAction = eventQueue.Dequeue(); // Read from front of queue
                            }
                        }
                        else
                        {  // Invalid state change found
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // The sensor sample values have no useful data in this phase.
                            eventAction.eventType = EventType.ErrorEvent;
                            eventAction.actionType = ActionType.ReportStateError;
                            eventAction.eventStartHeaderIndex = readIndex;
                        }
                        break;

                    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    case StimPhase.ActiveStim:

                        if (nextState == currentState)
                        {
                            // Stimulus still active. Check if different set of keys are now flashing.
                            nextStimCode = sensorData.packetHeaderTable[readIndex].stimulusCode;
                            numStimulusGroups = sensorData.packetHeaderTable[readIndex].numStimulusGroups;
                            lastSampleIndex = sensorData.packetHeaderTable[readIndex].crossRefIndex;

                            if ((0 <= nextStimCode) & (nextStimCode <= numStimulusGroups))
                            {   // Stimulus code is within expected range of values.

                                // When the keys in the stimulus group are "flashed ON", then stimulusCode is
                                // a non-zero value. When keys in group are not flashed, then stimulusCode = 0.
                                // This was done this way to be compatible with BCI2000 utility.
                                if ((nextStimCode == currentStimCode) | (nextStimCode == 0))
                                {
                                    // Next stimulus code value not found yet. Advance to next packet header.
                                    eventFound = false;
                                    readIndex = (++readIndex) & (PacketizedSensorData.HEADER_CIRC_BUF_LEN - 1);
                                }
                                else
                                {
                                    // Found next new stimulus code value.
                                    eventFound = true;
                                    currentStimCode = nextStimCode;

                                    // Save location where we stopped search
                                    sensorData.headerTableReadIndex = readIndex;

                                    // What happens next depends on how many sensor samples have been buffered
                                    // since the flashing stimulus has started. Need a preset length of buffered 
                                    // samples to match the window length of the weight coefficients before you 
                                    // can start computing weighted scores used to make a Prediction.

                                    // Take a look at the stim code event at the front of the queue.
                                    queuedEvent = eventQueue.Peek();

                                    // Get the index into the sampleValues[] array associated with the packet
                                    // header for the event.
                                    startIndex = queuedEvent.eventStartHeaderIndex;
                                    firstSampleIndex = sensorData.packetHeaderTable[startIndex].crossRefIndex;

                                    // Determine the number of buffered sample values between then and current event
                                    if (lastSampleIndex > firstSampleIndex)
                                    {
                                        numBufferedSamples = lastSampleIndex - firstSampleIndex;
                                    }
                                    else
                                    {   // Latest numBufferedSamplespacket data wrapped around to start of SampleValues[] array.
                                        numBufferedSamples = (PacketizedSensorData.SAMPLE_CIRC_BUF_LEN - firstSampleIndex) + lastSampleIndex;
                                    }

                                    // Add the current event to the back of the queue.
                                    queuedEvent.eventType = EventType.StimCodeChange;
                                    queuedEvent.actionType = ActionType.ComputeScore;
                                    queuedEvent.eventStartHeaderIndex = readIndex;
                                    queuedEvent.windowLength = 0;       // This event not ready to compute score yet
                                    eventQueue.Enqueue(queuedEvent);

                                    // Decide what to do next based on the number of buffered samples.
                                    // If buffered enough samples to apply the weight coefficients to compute
                                    // the score for the flash interval, then fetch the event action from the
                                    // front of the queue to process next, otherwise indicate it is not time
                                    // to take any action yet.

                                    if (numBufferedSamples >= PRESET_WINDOW_LENGTH)
                                    {
                                        eventAction = eventQueue.Dequeue();     // Read from front of queue

                                        // Use preset value for window length to computed the weighted score
                                        eventAction.windowLength = PRESET_WINDOW_LENGTH;
                                    }
                                    else
                                    {
                                        // Fill eventAction record fields to indicate no action.
                                        eventAction.eventType = EventType.StimCodeChange;
                                        eventAction.actionType = ActionType.NoAction;
                                        eventAction.eventStartHeaderIndex = readIndex;
                                        eventAction.windowLength = 0;       // Not ready to compute score yet
                                    }
                                }  // Found next new stimulus code value.
                            }  // Stimulus code is within expected range of values.

                            else
                            {
                                // Out of range stimulus code value detected
                                eventFound = true;

                                // Save location where we stopped search
                                sensorData.headerTableReadIndex = readIndex;

                                // Error occurred between when packet header is created and reading it now.
                                eventAction.eventType = EventType.ErrorEvent;
                                eventAction.actionType = ActionType.ReportStimError;
                                eventAction.eventStartHeaderIndex = readIndex;
                                eventAction.windowLength = 0;
                            }
                        }  // if (nextState == currentState)

                        else if (nextState == StimPhase.PostStim)
                        {
                            // Next valid state found
                            currentState = nextState;
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // When state changes to PostStim, two events have actually occurred.
                            // First, put start of PostStim onto queue to determine state length later on.
                            queuedEvent.eventType = EventType.StateChange;
                            queuedEvent.actionType = ActionType.StimulusEnd;
                            queuedEvent.eventStartHeaderIndex = readIndex;
                            queuedEvent.windowLength = 0;       // Fill in value later
                            eventQueue.Enqueue(queuedEvent);    // Store at back of queue

                            // Second, return back queued event marking time to compute next score 
                            eventAction = eventQueue.Dequeue(); // Read from front of queue

                            // Set window length equal to length of weight coefficients
                            eventAction.windowLength = PRESET_WINDOW_LENGTH;
                        }

                        else if ((nextState == StimPhase.Halted) | (nextState == StimPhase.NoStim))
                        {
                            // Check if Dynamic stopping halted Prediction in middle of stimulus,
                            // i.e. there is an abrupt in change to Halted or NoStim
                            currentState = nextState;
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // The sensor sample values have no useful data in this phase.
                            eventAction.eventType = EventType.StateChange;
                            eventAction.actionType = ActionType.StimulusEnd;
                            eventAction.eventStartHeaderIndex = readIndex;
                        }
                        break;

                    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    case StimPhase.PostStim:
                        if (nextState == currentState)
                        {
                            // All we are trying to do in PostStim is to Dequeue one buffered stimulus code
                            // or state change event at a time per flash interval of received packetized data.
                            // Find the end of the most recent block of packets stored in header table
                            lastSampleIndex = sensorData.packetHeaderTable[readIndex].crossRefIndex;
                            readIndex = (++readIndex) & (PacketizedSensorData.HEADER_CIRC_BUF_LEN - 1);

                            // Check if any events are still in the queue.
                            if (eventQueue.Count != 0)
                            {
                                // Take a look at the stim code event at the front of the queue.
                                queuedEvent = eventQueue.Peek();

                                // Get the index into the sampleValues[] array associated with the packet
                                // header for the event.
                                startIndex = queuedEvent.eventStartHeaderIndex;
                                firstSampleIndex = sensorData.packetHeaderTable[startIndex].crossRefIndex;

                                // Determine the number of buffered sample values between now and start of event
                                if (lastSampleIndex > firstSampleIndex)
                                {
                                    numBufferedSamples = lastSampleIndex - firstSampleIndex;
                                }
                                else
                                {   // Latest numBufferedSamplespacket data wrapped around to start of SampleValues[] array.
                                    numBufferedSamples = (PacketizedSensorData.SAMPLE_CIRC_BUF_LEN - firstSampleIndex) + lastSampleIndex;
                                }

                                // Decide what to do next based on the number of buffered samples.
                                if (numBufferedSamples >= PRESET_WINDOW_LENGTH)
                                {
                                    // Still need to compute scores for queued flash intervals
                                    if (eventQueue.Count() > 1)
                                    {
                                        // Save location where we stopped search
                                        eventFound = true;
                                        sensorData.headerTableReadIndex = readIndex;

                                        // Return queued event indicating it's time to compute next score 
                                        eventAction = eventQueue.Dequeue(); // Read from front of queue

                                        // Set window length equal to length of weight coefficients
                                        eventAction.windowLength = PRESET_WINDOW_LENGTH;
                                    }
                                }
                                else
                                {
                                    // Fill eventAction record fields to indicate no action.
                                    eventAction.eventType = EventType.StimCodeChange;
                                    eventAction.actionType = ActionType.NoAction;
                                    eventAction.eventStartHeaderIndex = readIndex;
                                    eventAction.windowLength = 0;       // Not ready to compute score yet
                                }
                            } // if (eventQueue.Count != 0)
                        }

                        else if (nextState == StimPhase.NoStim)
                        {  // Next valid state found
                            currentState = nextState;
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // For Fixed Sequence stopping, flash stimulus is done. 
                            // Prediction will end shortly after last of buffered data is processed.
                            // NOTE: For Dynamic stopping, the stimulus keeps running until prediction is made.
                            eventAction.eventType = EventType.StateChange;
                            eventAction.actionType = ActionType.StimulusEnd;
                            eventAction.eventStartHeaderIndex = readIndex;
                        }
                        break;

                    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    case StimPhase.Halted:
                        // Flashing stimulus has stopped. It also serves as a reminder that
                        // we need to handle Dynamic Stopping and Pause/Resume in the future.

                        // However this is handled, the start of next target begins with NoStim state.
                        if (nextState == currentState)
                        {  // No change in state. Examine next packet header
                            readIndex = (++readIndex) & (PacketizedSensorData.HEADER_CIRC_BUF_LEN - 1);
                        }
                        else if ((nextState == StimPhase.NoStim) | (nextState == StimPhase.PostStim))
                        {  // Next valid state found
                            currentState = nextState;
                            eventFound = true;

                            // Save location where we stopped search
                            sensorData.headerTableReadIndex = readIndex;

                            // Return back event marking the beginning of NoStim state.
                            eventAction.eventType = EventType.StateChange;
                            eventAction.actionType = ActionType.MarkStateChange;
                            eventAction.eventStartHeaderIndex = readIndex;
                        }
                        break;

                }  // switch

            }  // while()

            if (eventFound == false)
            {
                // Save location where we stopped search
                sensorData.headerTableReadIndex = readIndex;

                // No state or stimulus code changes detected in this block of received packets
                eventAction.eventType = EventType.NoEvent;
                eventAction.actionType = ActionType.NoAction;
                eventAction.eventStartHeaderIndex = readIndex;
                eventAction.windowLength = 0;
            }

            return eventAction;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}