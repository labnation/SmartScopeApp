using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal abstract class FormEntryDefinition
    {
        public string Name;
        public object Value;
        public DrawableCallback ValueChangedCallback;
    }
    internal class FormEntryDefinitionDouble : FormEntryDefinition
    {
        public object Prefix;
        public object Suffix;
        public double MinValue = double.MinValue;
        public double MaxValue = double.MaxValue;        
    }
    internal class FormEntryDefinitionString : FormEntryDefinition
    {
        public int MaxNbrChars = Scaler.DefaultMaxNbrChars;
    }
    internal class FormEntryDefinitionPassword : FormEntryDefinitionString
    {
    }

    internal class EForm : SplitPanel
    {
        private Dictionary<string, int> entryMapper = new Dictionary<string, int>();
        private Dictionary<string, string> internalValues = new Dictionary<string, string>();
        private SplitPanel fieldsValues;
        private List<FormEntryDefinition> originalEntries;
        private DrawableCallback formSubmitCallback;
        private string defaultStringValue = "....";
        private object extraArgumentToBePassedBackByForm;

        public EForm() : base(Orientation.Vertical, 5, SizeType.Inches)
        {
        }

        public string GetValue(string entryName)
        {
            if (!entryMapper.ContainsKey(entryName))
                throw new Exception("ERROR: entryMapper does not contain key " + entryName);

            int indexer = entryMapper[entryName];
            string value = internalValues[entryName];
            FormEntryDefinitionDouble doubleType = originalEntries[indexer] as FormEntryDefinitionDouble;
            if (doubleType != null)
            {
                if (doubleType.Prefix != null)
                    value = value.Replace(doubleType.Prefix + " ", "");
                if (doubleType.Suffix != null)
                    value = value.Replace(" " + doubleType.Suffix, "");
            }
            return value;
        }

        public void SetValue(string entryName, object newValue)
        {
            if (!entryMapper.ContainsKey(entryName))
                throw new Exception("ERROR while setting value: entryMapper does not contain key " + entryName);

            //first parse value
            int indexer = entryMapper[entryName];
            string valueText = newValue.ToString();
            

            //then update value
            internalValues[entryName] = valueText;

            FormEntryDefinitionDouble doubleType = originalEntries[indexer] as FormEntryDefinitionDouble;
            if (doubleType != null)
                valueText = doubleType.Prefix + " " + valueText + " " + doubleType.Suffix;

            FormEntryDefinitionPassword passwType = originalEntries[indexer] as FormEntryDefinitionPassword;
            if (passwType != null)
                valueText = "[" + valueText.Length + " chars]";

            (fieldsValues.Panels[indexer] as EButtonImageAndText).Text = valueText;

            if (originalEntries[indexer].ValueChangedCallback != null)
                originalEntries[indexer].ValueChangedCallback.Call(this, valueText);
        }

        public void ChangeSuffix(string entryName, string newSuffix)
        {
            if (!entryMapper.ContainsKey(entryName))
                throw new Exception("ERROR while setting value: entryMapper does not contain key " + entryName);

            //first parse value
            int indexer = entryMapper[entryName];
            FormEntryDefinitionDouble doubleType = originalEntries[indexer] as FormEntryDefinitionDouble;
            if (doubleType == null)
                throw new Exception("No suffixes supported on this FormEntry");

            //update suffix
            (originalEntries[indexer] as FormEntryDefinitionDouble).Suffix = newSuffix;

            //update field value
            (fieldsValues.Panels[indexer] as EButtonImageAndText).Text = doubleType.Prefix + " " + internalValues[entryName] + " " + doubleType.Suffix; ;
        }

        public void Redefine(string title, string buttonText, List<FormEntryDefinition> entries, DrawableCallback formSubmitCallback, object extraArgumentToBePassedBackByForm)
        {
            originalEntries = entries; //needed to remember pre and suffixes for updating values later on
            entryMapper.Clear();
            this.formSubmitCallback = formSubmitCallback;
            float barHeight = Scaler.SideMenuHeight;
            this.extraArgumentToBePassedBackByForm = extraArgumentToBePassedBackByForm;       

            //title bar            
            EButtonImageAndText titleBar = new EButtonImageAndText(null, MappedColor.MenuSolidBackgroundVoid, title, MappedColor.MenuButtonForeground) { StretchTexture = true, TextPosition = Location.Center };
            this.SetPanel(0, titleBar);
            this.SetPanelSize(0, barHeight);

            //fields
            int nrFields = entries.Count;
            SplitPanel fieldsTitles = new SplitPanel(Orientation.Vertical, nrFields, SizeType.Inches);
            fieldsValues = new SplitPanel(Orientation.Vertical, nrFields, SizeType.Inches);
            for (int i = 0; i < nrFields; i++)
            {
                //show entry name
                fieldsTitles.SetPanel(i, new EButtonImageAndText(null, MappedColor.MenuSolidBackgroundVoid, entries[i].Name, MappedColor.MenuButtonForeground) { StretchTexture = true, TextPosition = Location.Center });
                fieldsTitles.SetPanelSize(i, barHeight);

                //show entry value
                DrawableCallback callback;
                ////first parse value
                string valueText = entries[i].Value.ToString();
                internalValues[entries[i].Name] = valueText;
                int maxChars = 0;
                FormEntryDefinitionDouble doubleType = entries[i] as FormEntryDefinitionDouble;
                FormEntryDefinitionString stringType = entries[i] as FormEntryDefinitionString;
                if (doubleType != null)
                {
                    valueText = doubleType.Prefix + " " + valueText + " " + doubleType.Suffix;
                    callback = new Drawables.DrawableCallback(UICallbacks.ShowNumpadFormFieldDouble, entries[i]);
                }
                else if (stringType != null)
                {// stringtype
                    callback = new Drawables.DrawableCallback(UICallbacks.ShowNumpadFormFieldStringAlfaNumeric, new object[] { stringType.Name, stringType.MaxNbrChars });
                    if (valueText == "") valueText = defaultStringValue;
                }
                else
                    throw new Exception("Need to implement formtype");

                ////then add drawable
                fieldsValues.SetPanel(i, new EButtonImageAndText(null, MappedColor.MenuSolidBackground, valueText, MappedColor.MenuButtonForeground) { StretchTexture = true, TextPosition = Location.Center, TapCallback = callback });
                fieldsValues.SetPanelSize(i, barHeight);

                //store in mapper, so values can be retrieved/updated later on
                entryMapper.Add(entries[i].Name, i);
            }            

            SplitPanel fieldsPanel = new Drawables.SplitPanel(Orientation.Horizontal, 2, SizeType.Relative);
            fieldsPanel.SetPanel(0, fieldsTitles);
            fieldsPanel.SetPanelSize(0, 0.5f);
            fieldsPanel.SetPanel(1, fieldsValues);
            fieldsPanel.SetPanelSize(1, 0.5f);

            this.SetPanel(1, fieldsPanel);
            this.SetPanelSize(1, (float)nrFields * barHeight);

            //button            
            EButtonImageAndText commitButton = new EButtonImageAndText(null, MappedColor.MenuSolidBackground, buttonText, MappedColor.MenuButtonForeground) { StretchTexture = true, TextPosition = Location.Center, TapCallback = new DrawableCallback(OnSubmitButtonTapped) };
            this.SetPanel(2, commitButton);
            this.SetPanelSize(2, barHeight);

            //cancel button
            EButtonImageAndText cancelButton = new EButtonImageAndText(null, MappedColor.MenuSolidBackground, "Cancel", MappedColor.MenuButtonForeground) { StretchTexture = true, TextPosition = Location.Center, TapCallback = new DrawableCallback(UICallbacks.HideForm) };
            this.SetPanel(3, cancelButton);
            this.SetPanelSize(3, barHeight);

            //filler
            EButtonImageAndText filler = new EButtonImageAndText(null, MappedColor.MenuSolidBackgroundVoid, "void", MappedColor.MenuSolidBackgroundVoid) { StretchTexture = true, TextPosition = Location.Center };
            this.SetPanel(4, filler);
        }

        private void OnSubmitButtonTapped(EDrawable sender, object arg)
        {
            bool allFieldsValid = true;

            //check whether any field contains an invalid value -> in that case show toast and close form
            foreach (var kvp in entryMapper)
            {
                if (originalEntries[kvp.Value] is FormEntryDefinitionString)
                {
                    string val = (string)GetValue(kvp.Key);
                    if (val == "" || val == defaultStringValue)
                    {
                        allFieldsValid = false;
                        (new DrawableCallback(UICallbacks.ShowToast, new object[] { "Entry field " + originalEntries[kvp.Value].Name + " cannot be left blank", 3000 })).Call();
                    }
                }
            }

            if (allFieldsValid && formSubmitCallback != null)
            {
                //for all fields: store name,value in dictionary and forward to callback
                Dictionary<string, string> outputMapper = new Dictionary<string, string>();
                foreach (var kvp in entryMapper)
                    outputMapper.Add(kvp.Key, GetValue(kvp.Key));

                formSubmitCallback.Call(this, new object[] { outputMapper, extraArgumentToBePassedBackByForm });
            }

            (new DrawableCallback(UICallbacks.HideForm, null)).Call();
        }
    }
}
