using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using LabNation.Interfaces;
using LabNation.Common;
using ESuite.Drawables;

namespace ESuite.DataProcessors
{
    class DataProcessorDecoder: IDataProcessor
    {
        private Dictionary<string, ChannelData> sourceChannelData;
        public bool IsTimeVariant { get { return false; } }
        public void Reset() { }
        public bool ProcessingCompletedSuccessfully = false;
        public Channel dataProcessorChannel { get; protected set; }
        private Array decoderOutput;

        private ScopeDataCollection lastScopeDataCollection;
        public IProcessor Processor { get; private set; }
        public List<string> ProcessorContextMenuOrder { get { return Processor.Description.ContextMenuOrder; } }
        private bool initialized = false;
        private Dictionary<string, object> ParameterValues;
        public Dictionary<string, object> ParameterValuesCopy { get { return new Dictionary<string, object>(this.ParameterValues); } } //enforces usage of UpdateParameterValues. copy of dict, not of values inside. avoids caller to add/remove items from original
        public Dictionary<string, Type> ChannelTypes { get { return CleanedInputTypes; } }
        public Dictionary<string, Channel> SourceChannelMapCopy { get { return new Dictionary<string, Channel>(this.SourceChannelMap); } } //enforces usage of UpdateChannelConfiguration. copy of dict, not of values inside. avoids caller to add/remove items from original
        private Dictionary<string, Channel> SourceChannelMap = new Dictionary<string, Channel>();
        public Dictionary<string, bool> InputIsNullable;
        public Dictionary<string, Type> CleanedInputTypes;


        public DataProcessorDecoder(IProcessor processor, int index, ScopeDataCollection lastScopeDataCollection)
        {
            this.lastScopeDataCollection = lastScopeDataCollection; //this is needed in order to allow best estimated mapping of input channels, based on existing togglerate
            this.Processor = processor;

            if (processor is IDecoder)
                dataProcessorChannel = new ProtocolDecoderChannel(processor.Description.ShortName + " " + index.ToString(), index, typeof(DecoderOutput), this); //FIXME!!!
            else if (processor is IOperatorAnalog)
                dataProcessorChannel = new OperatorAnalogChannel(processor.Description.ShortName + " " + index.ToString(), index, this);
            else if (processor is IOperatorDigital)
                dataProcessorChannel = new OperatorDigitalChannel(processor.Description.ShortName + " " + index.ToString(), index, this);
            else
                throw new Exception("Unknown plugin type");

            /* convert possible nullable types to underlying types and keep track */
            InputIsNullable = new Dictionary<string, bool>();
            CleanedInputTypes = new Dictionary<string, Type>();
            for (int i = 0; i < Processor.Description.InputWaveformTypes.Count; i++)
            {
                string currentInput = Processor.Description.InputWaveformTypes.Keys.ElementAt(i);
                Type underlyingType = Nullable.GetUnderlyingType(Processor.Description.InputWaveformTypes[currentInput]);
                if (underlyingType == null) //in case type is NOT nullable
                {
                    InputIsNullable.Add(currentInput, false);
                    CleanedInputTypes.Add(currentInput, Processor.Description.InputWaveformTypes[currentInput]);
                }
                else //in case type IS nullable
                {
                    InputIsNullable.Add(currentInput, true);
                    CleanedInputTypes.Add(currentInput, underlyingType);
                }
            }            

            /* set default values for parameters */
            ParameterValues = new Dictionary<string, object>();
            if (processor.Description.Parameters != null)
            {
                foreach (DecoderParameter p in processor.Description.Parameters)
                {
                    UpdateParameterValues(p.ShortName, p.DefaultValue);
                }
            }
        }

        public void UpdateParameterValues(string key, object value)
        {
            ParameterValues[key] = value;

            //this lines makes sure decoder is reprocessed each time source input is changed, even when acquisition is stopped
            Process(lastScopeDataCollection, true);
        }

        public void UpdateSourceChannel(string sourceName, Channel ch)
        {
            Channel oldSourceChannel = null;
            //if sourceName was already provided: free the old channel
            if (SourceChannelMap.ContainsKey(sourceName))
            {
                //find corresponding channel
                oldSourceChannel = SourceChannelMap[sourceName];

                //DON'T delete link, as Engine could be calling Process() while this method is adding/removing elements to/from SourceChannelMap. entry will be replaced later on, which is thread-safe
                //SourceChannelMapLocalCopy.Remove(sourceName);
            }

            //if sourceName is already provided by other channel, we need to swap
            //but not when a channel is set to null! because more than 1 channel should be nullable
            //also only for Decoders, not for Operators 
            //I.e. setting SCL to chA while chA is currently used as SDA and SCL is configured for chB
            // then sourceName = 'SCL' and ch = 'chA'
            if ((oldSourceChannel != null) && (ch != null) && SourceChannelMap.ContainsValue(ch) && (Processor is IDecoder))
            {
                //Channel oldSourceChannel = SourceChannelMap[sourceName]; //example above: chB                
                string oldSourceName = SourceChannelMap.Single(x => x.Value == ch).Key;

                if (
                //before swapping, make sure underlying types are the same
                CleanedInputTypes[oldSourceName] == CleanedInputTypes[sourceName]
                &&
                //respect nullable-ness of channel
                (oldSourceChannel != null || InputIsNullable[oldSourceName])
                )
                {
                    SourceChannelMap[oldSourceName] = oldSourceChannel;
                }
            }

            if (SourceChannelMap.ContainsKey(sourceName))
                SourceChannelMap[sourceName] = ch;
            else
                SourceChannelMap.Add(sourceName, ch);

            //20190507: not sure why this needs to be duplicated?
            sourceChannelData = new Dictionary<string, ChannelData>();
            foreach (var kvp in SourceChannelMap)
                sourceChannelData[kvp.Key] = new ChannelData(ChannelDataSourceScope.Viewport, kvp.Value, null, false, 0, 0);

            Process(lastScopeDataCollection, true);
        }

        public bool BindExistingChannelsToInput()
        {
            //nullable sources don't HAVE to have an input
            //therefore: first make a dictionary with the mandatory inputs first, and nullables at the end
            List<string> toggleSpecifiedList = new List<string>();
            List<string> toggleNotSpecifiedList = new List<string>();
            List<string> nullableList = new List<string>();
            foreach (var kvp in CleanedInputTypes)
            {
                if (!InputIsNullable[kvp.Key])
                    if ((Processor.Description.InputWaveformExpectedToggleRates != null) && (Processor.Description.InputWaveformExpectedToggleRates.ContainsKey(kvp.Key)))
                        toggleSpecifiedList.Add(kvp.Key);
                    else
                        toggleNotSpecifiedList.Add(kvp.Key);
                else
                    nullableList.Add(kvp.Key);
            }

            //now sort wavesforms for which togglerate has been specified
            toggleSpecifiedList.OrderBy(waveName => Processor.Description.InputWaveformExpectedToggleRates[waveName]);

            //append all together
            List<string> priorityList = new List<string>();
            priorityList.AddRange(toggleSpecifiedList);
            priorityList.AddRange(toggleNotSpecifiedList);
            priorityList.AddRange(nullableList);

            //sort digital channels according to activity
            Dictionary<Channel, long> digitalActivity = Utils.GetActivityOfAllDigitizableChannels(lastScopeDataCollection);

            /* Find suiting input waves and bind them to decoder input */
            foreach (string sourceName in priorityList)
            {
                //find all enabled channels with matching type
                List<Channel> matchingChannels = new List<Channel>();                
                matchingChannels.AddRange(Utils.GetEnabledChannelsOfRequestedType(CleanedInputTypes[sourceName]));

                //if there are not enough channels of the native type found, see if we can use other waves
                if (matchingChannels.Count < priorityList.Count)
                    if (CleanedInputTypes[sourceName] == typeof(bool))
                        matchingChannels.AddRange(Utils.GetEnabledChannelsOfRequestedType(typeof(float)));

                //sort them by digital activity (doesn't harm in case of float input source)
                List<Channel> matchingChannelsSortedByActivity = new List<Channel>();
                if (digitalActivity != null)
                {
                    foreach (var kvp in digitalActivity.OrderBy(x => -x.Value))
                    {
                        if (matchingChannels.Contains(kvp.Key))
                        {
                            matchingChannelsSortedByActivity.Add(kvp.Key);
                            matchingChannels.Remove(kvp.Key);
                        }
                    }
                }
                matchingChannelsSortedByActivity.AddRange(matchingChannels); //matching channels contains the leftovers

                //remove channels which have already been assigned to previously matched inputs for this decoder
                for (int i = matchingChannelsSortedByActivity.Count - 1; i >= 0; i--)//count backwards when removing elements from a list
                    foreach (KeyValuePair<string, Channel> kvp2 in SourceChannelMapCopy)
                        if (matchingChannelsSortedByActivity.Count > i) //check needed, as element [i] might have been removed by the following code
                            if (matchingChannelsSortedByActivity[i] == kvp2.Value)
                                matchingChannelsSortedByActivity.RemoveAt(i);

                if (matchingChannelsSortedByActivity.Count > 0)
                {
                    //matching channel found
                    UpdateSourceChannel(sourceName, matchingChannelsSortedByActivity[0]);
                } else
                {
                    //when no matching channel was found : only problem for non-nullable sources
                    if (InputIsNullable[sourceName])
                        UpdateSourceChannel(sourceName, null);
                    else
                        return false; // report failure
                }                
            }

            //allow Process method to pass data on to decoder
            initialized = true;
            //report success            
            return true;
        }

        public void Process(ScopeDataCollection scopeDataCollection)
        {
            Process(scopeDataCollection, false);
        }

        public void Process(ScopeDataCollection scopeDataCollection, bool evenUpdateWhenDataIsUnchanged)
        {
            //safety check
            if (scopeDataCollection == null) return;

            //make sure we're no processing data twice
            if (!evenUpdateWhenDataIsUnchanged && scopeDataCollection == lastScopeDataCollection)
                return;

            //more safety checks
            if (!initialized) return;

            if (SourceChannelMapCopy.Count != CleanedInputTypes.Count)
            {
                Logger.Error("Not enough input waves received for digital decoder");
                return;
            }

            //fetch digital input waves, required by decoder
            Dictionary<string, Array> decodesInputWaves = new Dictionary<string, Array>();
            double samplePeriod = 0;

            //Update input and only proceed if a change in input was found
            int commLength = 0;
            ChannelData nonNullChannelData = null;
            foreach (var kvp in SourceChannelMapCopy)
            {
                if (kvp.Value != null)
                {
                    ChannelData d = scopeDataCollection.GetBestData(kvp.Value);
                    if (!sourceChannelData.ContainsKey(kvp.Key))
                        return;
                    if (d == null)
                        return;
                    sourceChannelData[kvp.Key] = d;
                    commLength = d.array.Length;
                    nonNullChannelData = d;
                }
                else //nullable waves
                {
                    sourceChannelData[kvp.Key] = null;
                }
            }

            if (nonNullChannelData == null) return;

            try
            {
                foreach (KeyValuePair<string, Channel> source in SourceChannelMapCopy)
                {
                    Type typeRequestedByDecoder = CleanedInputTypes[source.Key];
                    ChannelData channelData = sourceChannelData[source.Key];

                    //in case of nullable input which is disabled: add array of default values for that type and switch to next
                    if ((channelData == null) && InputIsNullable[source.Key])
                    {
                        if (CleanedInputTypes[source.Key] == typeof(float))
                            decodesInputWaves.Add(source.Key, new float[commLength]);
                        else if (CleanedInputTypes[source.Key] == typeof(bool))
                            decodesInputWaves.Add(source.Key, new bool[commLength]);
                        else
                            throw new Exception("No default array for this nullable type defined");
                        continue;
                    }

                    Array channelDataArray = (Array)channelData.array;

                    //FIXME!! this shouldn't happen. happens when I2C0, FPGA1, I2C2 is added. then I2C0 removed. Then new FPGA added -> crash
                    if (channelDataArray == null) return;

                    //default data conversions, to increase decoder compatibility
                    if ((typeRequestedByDecoder == typeof(bool)) && (channelDataArray.GetType().GetElementType() == typeof(float)))
                        channelDataArray = LabNation.Common.Utils.Schmitt((float[])channelDataArray);

                    if (channelDataArray.GetType().GetElementType() != typeRequestedByDecoder)
                        throw new Exception("Decoder input array does not contain the requested type!");

                    decodesInputWaves.Add(source.Key, channelDataArray);
                    samplePeriod = channelData.samplePeriod;
                }
            }
            catch (Exception e)
            {
                Logger.Error("Aborting processing of decoder [" + this.Processor.Description.Name + "] due to error in type conversion");
                return;
            }

            //FIXME: this shouldn't happen either. but it does when decoder is added in analog mode, and then switching to LA mode
            foreach (KeyValuePair<string, Array> kvp in decodesInputWaves)
                if (kvp.Value != null) //for nullable waves
                    if (kvp.Value.Length != commLength)
                        return;

            //and throw to decoder to do its magic
            decoderOutput = DecodeWrapped(decodesInputWaves, ParameterValues, samplePeriod);

            //ensure result is stored in ScopeDataCollection
            //FIXME: datacollection should get a source named to this decoder
            scopeDataCollection.SetData(nonNullChannelData.source, dataProcessorChannel, decoderOutput);

            //store incoming data so source channels can be changed on the fly when acquisistion is stopped
            this.lastScopeDataCollection = scopeDataCollection;
            ProcessingCompletedSuccessfully = true;
        }

        //wrapped method to ease stackbased debugging
        private Array DecodeWrapped(Dictionary<string, Array> inputWaveforms, Dictionary<string, object> parameters, double samplePeriod)
        {
            if (Processor is IDecoder)
                return (Processor as IDecoder).Process(inputWaveforms, parameters, samplePeriod);
            else if (Processor is IOperatorAnalog)
                return (Processor as IOperatorAnalog).Process(inputWaveforms, parameters, samplePeriod);
            else if (Processor is IOperatorDigital)
                return (Processor as IOperatorDigital).Process(inputWaveforms, parameters, samplePeriod);
            else
                throw new Exception("Plugin type support not implemented");
        }
    }
}
