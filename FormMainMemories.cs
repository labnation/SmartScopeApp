#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LabNation.DeviceInterface;
using LabNation.DeviceInterface.Memories;
using System.Reflection;
using LabNation.Common;

namespace ESuite
{
    public partial class FormMain : Form
    {
        private List<TextBox> textBoxes = new List<TextBox>();
        private class TextBoxTag
        {
            public DeviceMemory memory;
            public MemoryRegister register;
            public Button wrButton;
            public Button rdButton;
            public TextBoxTag(DeviceMemory memory, MemoryRegister register, Button wrButton, Button rdButton)
            {
                this.memory = memory;
                this.register = register;
                this.wrButton = wrButton;
                this.rdButton = rdButton;
            }
        }
        private void ClearMemories(Control container)
        {
            if (InvokeRequired)
                this.Invoke((MethodInvoker)delegate { this.ClearMemories(container); });

            container.Controls.Clear();
        }


        private void AddMemories(Control container)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { AddMemories(container); });
                return;
            }
            this.SuspendLayout();
            container.Controls.Clear();
            Control previousControl = null;
            Size requiredContainerSize = new Size(0, 0);

            //list groupboxes for all memories in the device
            List<DeviceMemory> memories = device.GetMemories();
            if (memories == null) return;
            foreach (DeviceMemory mem in memories)
            {
                GroupBox groupBox = new GroupBox();
                //have "Write all" and "Read all" buttons
                //for each register:
                //  ID, Name, textbox, "Wr" "Rd" buttons
                int methodYOffset = 20;
                int maxFormHeight = 700;
                foreach(MemoryRegister register in mem.Registers.Values)
                {
                    //add to form
                    AddGuiForRegister(groupBox, ref methodYOffset, register.Address, mem);

                    //hook update method to event which gets fired in case a memory register gets updated
                    //FIXME: make this an "invalidate" delegate and have UI update itself from memory
                    register.OnInternalValueChanged += RegisterValueChanged;
                }

                //increase height of containing groupbox
                PlaceMemoryGroupbox(container, groupBox, previousControl, device.Serial, mem, methodYOffset, maxFormHeight);

                //pass reference on, so next groupbox can be positioned below this one
                previousControl = groupBox;

                //updated required form size
                requiredContainerSize.Width = groupBox.Right;
                if (groupBox.Bottom > requiredContainerSize.Height)
                    requiredContainerSize.Height = groupBox.Bottom;
            }
            this.ResumeLayout(true);
        }

        //adds a groupbox for one memory implemented by the DeviceImplementation
        private void PlaceMemoryGroupbox(Control container, GroupBox groupBox, Control aboveControl, string name, DeviceMemory mem, int contentHeight, int maxFormHeight)
        {
            int margin = 3;
            int groupBoxWidth = 300;
            //position this groupbox relative to the previous one
            Point location;
            if (aboveControl == null)
                location = new Point(margin, margin);
            else
            {
                //Check if we have some space below
                if (aboveControl.Bottom + contentHeight < maxFormHeight)
                    location = new Point(aboveControl.Left, aboveControl.Bottom + margin);
                else // Put it to the right
                    location = new Point(aboveControl.Right + margin, margin);
            }

            groupBox.Location = location;
            groupBox.Size = new System.Drawing.Size(groupBoxWidth, contentHeight + margin);

            //name for groupbox: last section after the .
            string[] splitString = { "." };
            string[] splitResult = mem.ToString().Split(splitString, StringSplitOptions.None);
            groupBox.Text = name + " : " + splitResult[splitResult.Length - 1];

            if(InvokeRequired)
                this.Invoke((MethodInvoker) delegate {
                    container.Controls.Add(groupBox);
                });
            else
                container.Controls.Add(groupBox);
        }

        //adds one pair of ID, name, textbox, and buttons for a register
        private void AddGuiForRegister(Control parentControl, ref int methodYOffset, uint registerAddress, DeviceMemory mem)
        {
            int height = 20;
            int idWidth = 20;
            int labelWidth = 150;
            int textBoxWidth = 30;
            int buttonWidth = 40;
            //add ID       
            Label lblID = new Label();
            lblID.Text = mem.Registers[registerAddress].Address.ToString();
            lblID.Location = new Point(3, methodYOffset);
            lblID.Size = new System.Drawing.Size(idWidth, height);
            parentControl.Controls.Add(lblID);

            //add name     
            Label lblName = new Label();
            lblName.Text = mem.Registers[registerAddress].Name;
            lblName.Location = new Point(lblID.Right, methodYOffset);
            lblName.Size = new System.Drawing.Size(labelWidth, height);
            parentControl.Controls.Add(lblName);

            //add textbox
            TextBox textBox = new TextBox();
            //textBox.Text = eFunctionality.InternalValue.ToString("#.##");
            
            if(mem.Registers.Values.First().GetType().Equals(typeof(ByteRegister)))
                textBox.Text = ((ByteRegister)mem.Registers[registerAddress]).GetByte().ToString();
            else if (mem.Registers.Values.First().GetType().Equals(typeof(BoolRegister)))
                textBox.Text = ((BoolRegister)mem.Registers[registerAddress]).GetBool() ? "1" : "0";

            textBox.Click += new EventHandler(textBoxSelectAll);
            textBox.LostFocus += TextboxValueChanged;
            textBox.Size = new Size(textBoxWidth, height);
            textBox.Location = new Point(lblName.Right, methodYOffset);
            //textBox.Show();
            parentControl.Controls.Add(textBox);
            this.textBoxes.Add(textBox);

            //add Wr button
            Button wrButton = new Button();
            wrButton.Text = "Wr";
            wrButton.Tag = textBox; //store textbox in tag, so when clicked the value can be used
            wrButton.Click += WriteButtonClicked;
            wrButton.Size = new Size(buttonWidth, height);
            wrButton.Location = new Point(textBox.Right, methodYOffset);
            wrButton.TabStop = false;
            //wrButton.Show();
            parentControl.Controls.Add(wrButton);

            //add Rd button
            Button rdButton = new Button();
            rdButton.Text = "Rd";
            rdButton.Tag = textBox;
            rdButton.Click += ReadButtonClicked;
            rdButton.Size = new Size(buttonWidth, height);
            rdButton.Location = new Point(wrButton.Right, methodYOffset);
            rdButton.TabStop = false;
            //rdButton.Show();
            parentControl.Controls.Add(rdButton);

            textBox.Tag = new TextBoxTag(mem, mem.Registers[registerAddress], wrButton, rdButton);
            textBox.KeyDown += new KeyEventHandler(registerTextboxKeyhandler);
            //increment Y position
            methodYOffset += height; //putting this line here allows to have multiple parameters per method            
        }
        void textBoxSelectAll(object sender, EventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        void registerTextboxKeyhandler(object sender, KeyEventArgs e)
        {
            TextBox t = (TextBox)sender;
            TextBoxTag tag = (TextBoxTag)t.Tag;
            if (e.KeyCode == Keys.Return)
            {
                tag.wrButton.PerformClick();
                t.SelectAll();
            }
        }

        //method which is invoked each time one of the "Wr" buttons is clicked
        //value from correct textbox should be fetched, and send to the Write method of the correct memory
        private void WriteButtonClicked(object sender, EventArgs e)
        {
            //get all required object to read value, and invoke method
            Button button = (Button)sender;
            TextBox textBox = (TextBox)button.Tag;
            TextBoxTag t = (TextBoxTag)textBox.Tag;

            //invoke method
            object parsed = null;
            if(t.register.GetType().Equals(typeof(ByteRegister))) {
                byte bytevalue;
                byte.TryParse(textBox.Text, out bytevalue);
                parsed = bytevalue;
            }
            else if (t.register.GetType().Equals(typeof(BoolRegister)))
            {
                int bytevalue;
                int.TryParse(textBox.Text, out bytevalue);
                parsed = bytevalue > 0;
            }

            t.register.Set(parsed);
            try
            {
                Logger.Debug(String.Format("Immediately writing {0} to {1}:{2}({3}) of memory {2}", parsed, t.register.Memory.GetType().Name, t.register.Name, t.register.Address));
                t.memory[t.register.Address].WriteImmediate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't write: [" + ex.GetType().Name + "]" + ex.Message);
            }
        }

        //method which is invoked each time one of the "Rd" buttons is clicked
        //value from correct textbox should be fetched, and send to the Read method of the correct memory
        private void ReadButtonClicked(object sender, EventArgs e)
        {
            //get all required object to read value, and invoke method
            Button button = (Button)sender;
            TextBox textBox = (TextBox)button.Tag;
            TextBoxTag t = (TextBoxTag)textBox.Tag;

            try
            {
                t.register.Read();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't read: [" + ex.GetType().Name + "]" + ex.Message);
            }
        }

        //called each time a textbox value is updated
        private void TextboxValueChanged(object sender, EventArgs e)
        {
            //get all required object to read value, and invoke method
            //Button button = (Button)sender;
            //TextBox textBox = (TextBox)button.Tag;
            TextBox textBox = (TextBox)sender;
            TextBoxTag t = (TextBoxTag)textBox.Tag;

            //try to parse the value from the textbox
            float parsed;
            if (!float.TryParse(textBox.Text, out parsed))
                Logger.Error("could not parse textbox value into float " + textBox.Text + t.memory.GetType().ToString() + t.register.Address.ToString());

            //clamp value within range
            if (parsed > t.register.MaxValue) parsed = t.register.MaxValue;

            //update textbox
            //textBox.Text = parsed.ToString("#.##");
            textBox.Text = parsed.ToString();
        }

        //update method: called each time one of the register values is changed
        private void RegisterValueChanged(object sender, EventArgs e)
        {
            try
            {
                //scan through all items of this form. 
                //if the correct memory and regIndex was found, update the textbox value
                foreach (TextBox textBox in this.textBoxes)
                {
                    TextBoxTag t = (TextBoxTag)textBox.Tag;
                    if(t.register == sender)
                        UpdateRegisterTextbox(textBox);
                }
            }
            catch
            {
                Logger.Error("error in RegisterValueChanged method in FormMemories! " + sender.GetType().ToString());
            }
        }

        private delegate void UpdateRegisterTextboxDelegate(TextBox textBox);
        private static void UpdateRegisterTextbox(TextBox textBox)
        {
            TextBoxTag t = (TextBoxTag)textBox.Tag;
            if (textBox.InvokeRequired)
            {
                UpdateRegisterTextboxDelegate del = UpdateRegisterTextbox;
                textBox.Invoke(del, new object[] { textBox });
            }
            else
            {
                object register = t.register.Get();
                if (register.GetType().Equals(typeof(bool)))
                    textBox.Text = (bool)register ? "1" : "0";
                else
                    textBox.Text = t.register.Get().ToString();
            }
        }
    }
}
#endif