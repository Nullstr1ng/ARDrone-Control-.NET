﻿/* ARDrone Control .NET - An application for flying the Parrot AR drone in Windows.
 * Copyright (C) 2010 Thomas Endres, Stephen Hobley, Julien Vinel
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using ARDrone.Control;
using ARDrone.Detection;
using ARDrone.Input;
using Emgu.CV;
using Emgu.CV.Structure;

namespace ARDroneUI_Detection_Forms
{
    public partial class MainForm : Form
    {
        private delegate void OutputEventHandler(String output);

        //private DispatcherTimer timerStatusUpdate;
        //private DispatcherTimer timerVideoUpdate;

        private InputManager inputManager = null;
        private ARDroneControl arDroneControl = null;

        private SignDetector signDetector = null;
        private CourseAdvisor courseAdvisor = null;

        int frameCountSinceLastCapture = 0;
        DateTime lastFrameRateCaptureTime;
        int averageFrameRate = 0;

        String snapshotFilePath = string.Empty;

        public MainForm()
        {
            InitializeComponent();
            InitializeInputManager();

            arDroneControl = new ARDroneControl();

            signDetector = new SignDetector();
            courseAdvisor = new CourseAdvisor(arDroneControl.BottomCameraPictureSize, arDroneControl.BottomCameraFieldOfViewDegrees);
        }

        public void DisposeControl()
        {
            inputManager.Dispose();
        }

        public void InitializeInputManager()
        {
            inputManager = new ARDrone.Input.InputManager(this.Handle);
            AddInputListeners();
        }

        private void AddInputListeners()
        {
            inputManager.NewInputState += new NewInputStateHandler(inputManager_NewInputState);
        }

        private void RemoveInputListeners()
        {
            inputManager.NewInputState -= new NewInputStateHandler(inputManager_NewInputState);
        }

        public void Init()
        {
            timerStatusUpdate.Start();

            UpdateStatus();
            UpdateInteractiveElements();
        }

        private void Connect()
        {
            if (!arDroneControl.CanConnect) { return; }

            if (arDroneControl.Connect())
            {
                arDroneControl.ChangeCamera(); // Bottom camera
                UpdateUISync("Connected to Drone");
            }
            else
            {
                UpdateUISync("Error initializing drone");
            }

            timerVideoUpdate.Start();
            lastFrameRateCaptureTime = DateTime.Now;
        }

        private void Disconnect()
        {
            if (!arDroneControl.CanDisconnect) { return; }

            timerVideoUpdate.Stop();

            if (arDroneControl.Shutdown())
            {
                UpdateUIAsync("Shutdown Drone");
            }
            else
            {
                UpdateUIAsync("Error shutting down Drone");
            }
        }

        private void ChangeCamera()
        {
            if (!arDroneControl.CanChangeCamera) { return; }

            arDroneControl.ChangeCamera();
            UpdateUIAsync("Changing camera");
        }

        private void Takeoff()
        {
            if (!arDroneControl.CanTakeoff) { return; }

            arDroneControl.Takeoff();
            UpdateUIAsync("Taking off");
        }

        private void Land()
        {
            if (!arDroneControl.CanLand) { return; }

            arDroneControl.Land();
            UpdateUIAsync("Landing");
        }

        private void Emergency()
        {
            if (!arDroneControl.CanCallEmergency) { return; }

            arDroneControl.Emergency();
            UpdateUIAsync("Emergency button hit");
        }

        private void FlatTrim()
        {
            if (!arDroneControl.CanSendFlatTrim) { return; }

            arDroneControl.FlatTrim();
            UpdateUIAsync("Sending flat trim");
        }

        private void EnterHoverMode()
        {
            if (!arDroneControl.CanEnterHoverMode) { return; }

            arDroneControl.EnterHoverMode();
            UpdateUIAsync("Entering hover mode");
        }

        private void LeaveHoverMode()
        {
            if (!arDroneControl.CanLeaveHoverMode) { return; }

            arDroneControl.LeaveHoverMode();
            UpdateUIAsync("Leaving hover mode");
        }

        private void Navigate(float roll, float pitch, float yaw, float gaz)
        {
            if (!arDroneControl.CanFlyFreely) { return; }

            arDroneControl.SetFlightData(roll, pitch, gaz, yaw);
        }

        private void UpdateUIAsync(String message)
        {
            this.BeginInvoke(new OutputEventHandler(UpdateUISync), message);
        }

        private void UpdateUISync(String message)
        {
            textBoxOutput.AppendText(message + "\r\n");

            UpdateInteractiveElements();
        }

        private void UpdateInteractiveElements()
        {
            inputManager.SetFlags(arDroneControl.IsConnected, arDroneControl.IsEmergency, arDroneControl.IsFlying, arDroneControl.IsHovering);

            if (arDroneControl.CanConnect) { buttonConnect.Enabled = true; } else { buttonConnect.Enabled = false; }
            if (arDroneControl.CanDisconnect) { buttonShutdown.Enabled = true; } else { buttonShutdown.Enabled = false; }

            if (arDroneControl.CanTakeoff || arDroneControl.CanLand) { buttonCommandTakeoff.Enabled = true; } else { buttonCommandTakeoff.Enabled = false; }
            if (arDroneControl.CanEnterHoverMode || arDroneControl.CanLeaveHoverMode) { buttonCommandHover.Enabled = true; } else { buttonCommandHover.Enabled = false; }
            if (arDroneControl.CanCallEmergency) { buttonCommandEmergency.Enabled = true; } else { buttonCommandEmergency.Enabled = false; }
            if (arDroneControl.CanSendFlatTrim) { buttonCommandFlatTrim.Enabled = true; } else { buttonCommandFlatTrim.Enabled = false; }

            if (!arDroneControl.IsFlying) { buttonCommandTakeoff.Text = "Take off"; } else { buttonCommandTakeoff.Text = "Land"; }
            if (!arDroneControl.IsHovering) { buttonCommandHover.Text = "Start hover"; } else { buttonCommandHover.Text = "Stop hover"; }
        }

        private void UpdateStatus()
        {
            if (!arDroneControl.IsConnected)
            {
                labelCamera.Text = "No picture";
                labelStatusCamera.Text = "None";

                labelStatusBattery.Text = "N/A";
                labelStatusAltitude.Text = "N/A";

                labelStatusPitch.Text = "+0.0000°";
                labelStatusRoll.Text = "+0.0000°";
            }
            else
            {
                ARDroneControl.DroneData data = new ARDroneControl.DroneData();
                data = arDroneControl.GetCurrentDroneData();
                int frameRate = GetCurrentFrameRate();

                if (arDroneControl.CurrentCameraType == ARDroneControl.CameraType.FrontCamera)
                {
                    labelCamera.Text = "Front camera";
                    labelStatusCamera.Text = "Front";
                }
                else
                {
                    labelCamera.Text = "Bottom camera";
                    labelStatusCamera.Text = "Bottom";
                }

                labelStatusBattery.Text = data.BatteryLevel.ToString() + "%";
                labelStatusAltitude.Text = data.Altitude.ToString();

                labelStatusPitch.Text = String.Format("{0:+0.000;-0.000;+0.000}", data.Theta);
                labelStatusRoll.Text = String.Format("{0:+0.000;-0.000;+0.000}", data.Phi);
            }


            labelStatusConnected.Text = arDroneControl.IsConnected.ToString();
            labelStatusFlying.Text = arDroneControl.IsFlying.ToString();
            labelStatusHovering.Text = arDroneControl.IsHovering.ToString();
        }

        private int GetCurrentFrameRate()
        {
            int timePassed = (int)(DateTime.Now - lastFrameRateCaptureTime).TotalMilliseconds;
            int frameRate = frameCountSinceLastCapture * 1000 / timePassed;
            averageFrameRate = (averageFrameRate + frameRate) / 2;

            lastFrameRateCaptureTime = DateTime.Now;
            frameCountSinceLastCapture = 0;

            return averageFrameRate;
        }

        private void SendDroneCommands(InputState inputState)
        {
            if (inputState.CameraSwap)
            {
                ChangeCamera();
            }

            if (inputState.TakeOff && arDroneControl.CanTakeoff)
            {
                Takeoff();
            }
            else if (inputState.Land && arDroneControl.CanLand)
            {
                Land();
            }

            if (inputState.Hover && arDroneControl.CanEnterHoverMode)
            {
                EnterHoverMode();
            }
            else if (inputState.Hover && arDroneControl.CanLeaveHoverMode)
            {
                LeaveHoverMode();
            }

            if (inputState.Emergency)
            {
                Emergency();
            }
            else if (inputState.FlatTrim)
            {
                FlatTrim();
            }

            float roll = inputState.Roll / 1.0f;
            float pitch = inputState.Pitch / 1.0f;
            float yaw = inputState.Yaw / 2.0f;
            float gaz = inputState.Gaz / 2.0f;

            Navigate(roll, pitch, yaw, gaz);
        }

        private void UpdateVideoImage()
        {
            if (arDroneControl.IsConnected)
            {
                System.Drawing.Bitmap newImage = (System.Drawing.Bitmap)arDroneControl.GetDisplayedImage();

                PerformStopSignDetection(newImage);
                UpdateVisualImage(newImage);
            }
        }

        private void PerformStopSignDetection(System.Drawing.Bitmap image)
        {
            List<SignDetector.SignResult> results = DetermineAndMarkStopSignsInVideoSignal(image);
            DetermineAndMarkAdvisedCourse(results);
        }

        private void UpdateVisualImage(System.Drawing.Bitmap image)
        {
            if (image != null)
            {
                frameCountSinceLastCapture++;

                pictureBoxVideo.Image = image;
            }
        }

        private List<SignDetector.SignResult> DetermineAndMarkStopSignsInVideoSignal(System.Drawing.Bitmap image)
        {
            Image<Bgr, Byte> imageToProcess = new Image<Bgr, Byte>(image);
            Image<Gray, Byte> maskedImage;

            List<SignDetector.SignResult> results = signDetector.DetectStopSign(imageToProcess, out maskedImage);

            for (int i = 0; i < results.Count; i++)
            {
                image = (System.Drawing.Bitmap)DrawingUtilities.DrawRectangleToImage(image, results[i].Rectangle, System.Drawing.Color.White);
            }

            //TODO
            //imageMask.Source = DetectionUtils.ConvertImageToBitmapSource(maskedImage);

            return results;
        }

        private void DetermineAndMarkAdvisedCourse(List<SignDetector.SignResult> results)
        {
            CourseAdvisor.Direction advisedDirection = DetermineAdvisedCourse(results);
            MarkAdvisedCourse(advisedDirection);
        }

        private CourseAdvisor.Direction DetermineAdvisedCourse(List<SignDetector.SignResult> results)
        {
            ARDroneControl.DroneData droneData = arDroneControl.GetCurrentDroneData();
            return courseAdvisor.GetNavigationAdvice(results, droneData.Phi, droneData.Theta);
        }

        private void MarkAdvisedCourse(CourseAdvisor.Direction direction)
        {
            if (!direction.AdviceGiven || (direction.DeltaX == 0 && direction.DeltaY == 0))
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.None;
            }
            else if (direction.DeltaX == 0.0 && direction.DeltaY == 1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.Up;
            }
            else if (direction.DeltaX == 1.0 && direction.DeltaY == 1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.UpRight;
            }
            else if (direction.DeltaX == 1.0 && direction.DeltaY == 0.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.Right;
            }
            else if (direction.DeltaX == 1.0 && direction.DeltaY == -1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.DownRight;
            }
            else if (direction.DeltaX == 0.0 && direction.DeltaY == -1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.Down;
            }
            else if (direction.DeltaX == -1.0 && direction.DeltaY == -1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.DownLeft;
            }
            else if (direction.DeltaX == -1.0 && direction.DeltaY == 0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.Left;
            }
            else if (direction.DeltaX == -1.0 && direction.DeltaY == 1.0)
            {
                directionControl.ArrowDirection = DirectionControl.DirectionControl.Direction.UpLeft;
            }
        }

        private bool CanCaptureVideo
        {
            get
            {
                return arDroneControl.CanChangeCamera;
            }
        }

        // Event handlers

        private void Form_Load(object sender, EventArgs e)
        {
            Init();
        }

        private void Form_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DisposeControl();
            Disconnect();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private void buttonShutdown_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void buttonCommandChangeCamera_Click(object sender, EventArgs e)
        {
            ChangeCamera();
        }

        private void buttonCommandTakeoff_Click(object sender, EventArgs e)
        {
            if (!arDroneControl.IsFlying)
            {
                Takeoff();
            }
            else
            {
                Land();
            }
        }

        private void buttonCommandHover_Click(object sender, EventArgs e)
        {
            if (!arDroneControl.IsHovering)
            {
                EnterHoverMode();
            }
            else
            {
                LeaveHoverMode();
            }
        }

        private void buttonCommandEmergency_Click(object sender, EventArgs e)
        {
            Emergency();
        }

        private void buttonCommandFlatTrim_Click(object sender, EventArgs e)
        {
            FlatTrim();
        }

        private void timerStatusUpdate_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void timerVideoUpdate_Tick(object sender, EventArgs e)
        {
            UpdateVideoImage();
        }

        private void inputManager_NewInputState(object sender, NewInputStateEventArgs e)
        {
            SendDroneCommands(e.CurrentInputState);
            this.BeginInvoke(new NewInputStateHandler(inputManagerSync_NewInputState), this, e);

            Console.WriteLine(e.CurrentInputState.ToString());
        }

        private void inputManagerSync_NewInputState(object sender, NewInputStateEventArgs e)
        {

        }

        private int minValue = 12;
        private int maxValue = 160;

        private void checkBoxThresholdInvert_CheckedChanged(object sender, EventArgs e)
        {
            if (signDetector != null)
            {
                signDetector.invertChannel = checkBoxThresholdInvert.Checked;
            }
        }

        private void sliderThresholdMin_Scroll(object sender, EventArgs e)
        {
            if (signDetector != null)
            {
                minValue = (int)sliderThresholdMin.Value;
                signDetector.channelSliderMin = minValue;
                labelThreshold.Text = minValue.ToString() + "..." + maxValue.ToString();
            }
        }

        private void sliderThresholdMax_Scroll(object sender, EventArgs e)
        {
            if (signDetector != null)
            {
                maxValue = (int)sliderThresholdMax.Value;
                signDetector.channelSliderMax = maxValue;
                labelThreshold.Text = minValue.ToString() + "..." + maxValue.ToString();
            }
        }
    }
}