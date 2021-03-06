﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ProcessMemoryReaderLib;

namespace FPSHack
{
    public partial class Form1 : Form
    {
        //Imports RegisterHotKey method
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr windowHandle, int hotkeyIdentifier, uint modifierCode, uint keyCode);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);

        //Creates enumerator for the modifier keys
        [Flags]
        public enum Modifiers : uint
        {
            None = 0x0000,
            Alt = 0x0001,
            Control = 0x0002,
            Shift = 0x0004,
            Win = 0x0008
        }
        
        IntPtr xPosAddr;
        IntPtr yPosAddr;
        IntPtr zPosAddr;
        IntPtr enemyXPosAddr;
        IntPtr enemyYPosAddr;
        IntPtr enemyZPosAddr;
        IntPtr yVelocityAddr;
        IntPtr xMouseAddr;
        IntPtr writableXMouseAddr;
        IntPtr yMouseAddr;
        IntPtr writableYMouseAddr;
        IntPtr healthAddr;
        IntPtr primaryAmmoAddr;
        IntPtr primaryAmmoAddrPointer;

        public Form1()
        {

            InitializeComponent();

            #region Hotkey reg
            RegisterHotKey(this.Handle, 1, (uint)Modifiers.None, (uint)Keys.NumPad1);
            RegisterHotKey(this.Handle, 2, (uint)Modifiers.None, (uint)Keys.D1);
            RegisterHotKey(this.Handle, 3, (uint)Modifiers.Shift, (uint)Keys.Space);
            RegisterHotKey(this.Handle, 4, (uint)Modifiers.Control, (uint)Keys.Space);
            RegisterHotKey(this.Handle, 5, (uint)Modifiers.None, (uint)Keys.NumPad2);
            RegisterHotKey(this.Handle, 6, (uint)Modifiers.None, (uint)Keys.D2);
            RegisterHotKey(this.Handle, 7, (uint)Modifiers.None, (uint)Keys.Up);
            RegisterHotKey(this.Handle, 8, (uint)Modifiers.None, (uint)Keys.H);
            RegisterHotKey(this.Handle, 9, (uint)Modifiers.None, (uint)Keys.J);
            #endregion
        }
        
        ProcessMemoryReader pReader = new ProcessMemoryReader();

        float slope;
        float yawAngle;
        float pitchAngle;
        float playerYaw;
        float playerPitch;
        float playerHeight;
        float enemyHeight;
        float heightDifference;
        float distance;
        float readWriteXMouseDiff;
        float readWriteYMouseDiff;

        private void timer1_Tick(object sender, EventArgs e)
        {
            //All of this code is what shows the player's position in the app window.
            IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

            IntPtr bytesRead = (IntPtr)0;
            byte[] buffer = new byte[8];
            uint size = (uint)buffer.Length;

            ProcessMemoryReaderApi.ReadProcessMemory(processHandle, xPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            xPosLabel.Text = BitConverter.ToSingle(buffer, 0).ToString();

            ProcessMemoryReaderApi.ReadProcessMemory(processHandle, yPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            yPosLabel.Text = BitConverter.ToSingle(buffer, 0).ToString();

            ProcessMemoryReaderApi.ReadProcessMemory(processHandle, zPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            zPosLabel.Text = BitConverter.ToSingle(buffer, 0).ToString();

            ProcessMemoryReaderApi.ReadProcessMemory(processHandle, healthAddr, buffer, (uint)buffer.Length, out bytesRead);
            healthLabel.Text = "2147483647 / 100";

            //starting here is aimbot math
            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, xPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            float x1 = BitConverter.ToSingle(buffer, 0);

            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, enemyXPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            float x2 = BitConverter.ToSingle(buffer, 0);

            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, zPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            float z1 = BitConverter.ToSingle(buffer, 0);

            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, enemyZPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            float z2 = BitConverter.ToSingle(buffer, 0);

            slope = ((z2 - z1) / (x2 - x1));
                        
            yawAngle = (float)(Math.Atan2((z2 - z1), (x2 - x1)) * (180 / Math.PI));

            slopeLabel.Text = slope.ToString();
            angleLabel.Text = yawAngle.ToString();

            distance = (float)(Math.Sqrt(Math.Pow((x2 - x1), 2) + Math.Pow((z2 - z1), 2)));

            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, yPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            playerHeight = BitConverter.ToSingle(buffer, 0);
            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, enemyYPosAddr, buffer, (uint)buffer.Length, out bytesRead);
            enemyHeight = BitConverter.ToSingle(buffer, 0);

            heightDifference = playerHeight - enemyHeight;

            pitchAngle = (float)((Math.Atan2(heightDifference, distance)) * (180 / Math.PI));

            //This code gives the player infinite health
            byte[] healthValue = BitConverter.GetBytes(2147483647);
            ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, healthAddr, healthValue, 4, out bytesRead);

            //This code gives the player infinite primary ammo
            byte[] fourByteBuffer = new byte[4];
            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, primaryAmmoAddrPointer, fourByteBuffer, (uint)fourByteBuffer.Length, out bytesRead);
            primaryAmmoAddr = (IntPtr)(BitConverter.ToInt32(fourByteBuffer, 0) + 0x44C);

            byte[] ammoValue = BitConverter.GetBytes(2147483647);
            ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, primaryAmmoAddr, ammoValue, 4, out bytesRead);
        }

        byte[] savedXPos1;
        byte[] savedYPos1;
        byte[] savedZPos1;

        byte[] savedXPos2;
        byte[] savedYPos2;
        byte[] savedZPos2;

        bool aimbotIsOn = false;

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam == (IntPtr)1)
                {
                    IntPtr bytesRead;
                    byte[] bufferX = new byte[8];
                    byte[] bufferY = new byte[8];
                    byte[] bufferZ = new byte[8];
                                        
                    ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, xPosAddr, bufferX, (uint)bufferX.Length, out bytesRead);
                    savedXPos1 = bufferX;

                    ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, yPosAddr, bufferY, (uint)bufferY.Length, out bytesRead);
                    savedYPos1 = bufferY;

                    ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, zPosAddr, bufferZ, (uint)bufferZ.Length, out bytesRead);
                    savedZPos1 = bufferZ;

                }
                else if (m.WParam == (IntPtr)2)
                {
                    IntPtr store;

                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, xPosAddr, savedXPos1, 8, out store);
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, yPosAddr, savedYPos1, 8, out store);
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, zPosAddr, savedZPos1, 8, out store);
                }
                else if (m.WParam == (IntPtr)3)
                {
                    IntPtr bytesRead;
                    IntPtr store;

                    byte[] buffer = new byte[8];
                    ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, yPosAddr, buffer, (uint)buffer.Length, out bytesRead);
                    byte[] currentY = buffer;
                    float floatY = BitConverter.ToSingle(currentY, 0);
                    float addedY = floatY + 300;
                    byte[] newY = BitConverter.GetBytes(addedY);
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, yPosAddr, newY, 8, out store);

                    byte[] accelerationY = { 0x00000000 };
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, yVelocityAddr, accelerationY, 4, out store);
                }
                else if (m.WParam == (IntPtr)4)
                {
                    IntPtr store;

                    byte[] accelerationY = { 0x00 };
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, yVelocityAddr, accelerationY, 4, out store);
                }
                else if (m.WParam == (IntPtr)5)
                {
                    IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

                    IntPtr bytesRead = (IntPtr)0;
                    byte[] bufferX = new byte[8];
                    byte[] bufferY = new byte[8];
                    byte[] bufferZ = new byte[8];

                    ProcessMemoryReaderApi.ReadProcessMemory(processHandle, xPosAddr, bufferX, (uint)bufferX.Length, out bytesRead);
                    savedXPos2 = bufferX;

                    ProcessMemoryReaderApi.ReadProcessMemory(processHandle, yPosAddr, bufferY, (uint)bufferY.Length, out bytesRead);
                    savedYPos2 = bufferY;

                    ProcessMemoryReaderApi.ReadProcessMemory(processHandle, zPosAddr, bufferZ, (uint)bufferZ.Length, out bytesRead);
                    savedZPos2 = bufferZ;
                }
                else if (m.WParam == (IntPtr)6)
                {
                    IntPtr store = (IntPtr)0;

                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, xPosAddr, savedXPos2, 8, out store);
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, yPosAddr, savedYPos2, 8, out store);
                    ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, zPosAddr, savedZPos2, 8, out store);
                }
                else if (m.WParam == (IntPtr)7)
                {
                    byte[] buffer = new byte[8];
                    IntPtr store;
                    ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, xMouseAddr, buffer, 8, out store);
                    float direction = BitConverter.ToSingle(buffer, 0);

                    if (direction > -45 && direction < 45)
                    {
                        //xPos will increase
                        IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

                        IntPtr bytesRead = (IntPtr)0;
                        buffer = new byte[8];

                        ProcessMemoryReaderApi.ReadProcessMemory(processHandle, xPosAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float movement = BitConverter.ToSingle(buffer, 0) + 200;
                        byte[] finalMovement = BitConverter.GetBytes(movement);
                        ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, xPosAddr, finalMovement, (uint)finalMovement.Length, out bytesRead);
                    }
                    else if (direction < -45 && direction > -135)
                    {
                        //zPos will decrease
                        IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

                        IntPtr bytesRead = (IntPtr)0;
                        buffer = new byte[8];

                        ProcessMemoryReaderApi.ReadProcessMemory(processHandle, zPosAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float movement = BitConverter.ToSingle(buffer, 0) - 200;
                        byte[] finalMovement = BitConverter.GetBytes(movement);
                        ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, zPosAddr, finalMovement, (uint)finalMovement.Length, out bytesRead);
                    }
                    else if ((direction < -135 && direction > -179.99999999) || (direction > 135 && direction < 179.99999999))
                    {
                        //xPos will decrease
                        IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

                        IntPtr bytesRead = (IntPtr)0;
                        buffer = new byte[8];

                        ProcessMemoryReaderApi.ReadProcessMemory(processHandle, xPosAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float movement = BitConverter.ToSingle(buffer, 0) - 200;
                        byte[] finalMovement = BitConverter.GetBytes(movement);
                        ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, xPosAddr, finalMovement, (uint)finalMovement.Length, out bytesRead);
                    }
                    else if (direction > 45 && direction < 135)
                    {
                        //zPos will increase
                        IntPtr processHandle = ProcessMemoryReaderApi.OpenProcess(ProcessMemoryReaderApi.PROCESS_VM_READ, 0, (uint)process.Id);

                        IntPtr bytesRead = (IntPtr)0;
                        buffer = new byte[8];

                        ProcessMemoryReaderApi.ReadProcessMemory(processHandle, zPosAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float movement = BitConverter.ToSingle(buffer, 0) + 200;
                        byte[] finalMovement = BitConverter.GetBytes(movement);
                        ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, zPosAddr, finalMovement, (uint)finalMovement.Length, out bytesRead);
                    }
                }                
                else if (m.WParam == (IntPtr)8)
                {
                    if (aimbotIsOn == true)
                    {
                        timer3.Enabled = false;
                        aimbotIsOn = false;
                    }
                    else
                    {
                        timer3.Enabled = true;
                        aimbotIsOn = true;

                        //defining the difference between the readable and writable X address values.
                        IntPtr bytesRead;
                        byte[] buffer = new byte[8];

                        ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, xMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float readXMouseValue = BitConverter.ToSingle(buffer, 0);

                        ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, writableXMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float writeXMouseValue = BitConverter.ToSingle(buffer, 0);

                        readWriteXMouseDiff = readXMouseValue - writeXMouseValue;

                        //defining the difference between the readable and writable Y address value
                        ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, yMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float readYMouseValue = BitConverter.ToSingle(buffer, 0);

                        ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, writableYMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
                        float writeYMouseValue = BitConverter.ToSingle(buffer, 0);

                        readWriteYMouseDiff = readYMouseValue - writeYMouseValue;
                    }
                }
                else if (m.WParam == (IntPtr)9)
                {

                }
            }
            base.WndProc(ref m);
        }


        Process process;
        private void timer2_Tick(object sender, EventArgs e)
        {
            Process[] processList = Process.GetProcesses();

            for (int i = 0; i < processList.Length; i++)
            {
                if (processList[i].ProcessName == "iw4sp")
                {
                    process = processList[i];

                    label1.Text = "Mw2 MultiHack";
                    this.Text = "Mw2 MultiHack";

                    xPosAddr = (IntPtr)0x0108DC84;
                    yPosAddr = (IntPtr)0x0108DC8C;
                    zPosAddr = (IntPtr)0x0108DC88;
                    yVelocityAddr = (IntPtr)0x0108DC98;
                    xMouseAddr = (IntPtr)0x0108DD74;
                    healthAddr = (IntPtr)0x00EA7804;
                    //primaryAmmoAddr

                    break;
                }
                else if (processList[i].ProcessName == "iw3mp")
                {
                    process = processList[i];

                    label1.Text = "Cod4 MultiHack";
                    this.Text = "Cod4 MultiHack";

                    xPosAddr = (IntPtr)0x013255C4;
                    yPosAddr = (IntPtr)0x013255CC;
                    zPosAddr = (IntPtr)0x013255C8;
                    enemyXPosAddr = (IntPtr)0x01328748;
                    enemyYPosAddr = (IntPtr)0x01328750;
                    enemyZPosAddr = (IntPtr)0x0132874C;
                    //yVelocityAddr = (IntPtr)
                    xMouseAddr = (IntPtr)0x013256B4;
                    writableXMouseAddr = (IntPtr)0x00C84FDC;
                    yMouseAddr = (IntPtr)0x013256B0;
                    writableYMouseAddr = (IntPtr)0x00C84FD8;
                    healthAddr = (IntPtr)0x012886A0;
                    primaryAmmoAddr = (IntPtr)0x013255A8;

                    break;
                }
                else if (processList[i].ProcessName == "iw4mp")
                {
                    process = processList[i];

                    label1.Text = "Mw2 MultiHack";
                    this.Text = "Mw2 MultiHack";

                    xPosAddr = (IntPtr)0x01AA5504;
                    yPosAddr = (IntPtr)0x01AA5508;
                    zPosAddr = (IntPtr)0x01AA550C;
                    //yVelocityAddr = (IntPtr)
                    xMouseAddr = (IntPtr)0x01AA55F8;
                    //healthAddr = (IntPtr)
                    primaryAmmoAddrPointer = (IntPtr)0x0128865C;

                    break;
                }
            }

            if (process != null)
            {
                timer1.Enabled = true;
                timer4.Enabled = true;
                timer2.Enabled = false;
            }

        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            IntPtr bytesRead;
            byte[] buffer = new byte[8];

            //actual aimbot code for horizontal plane
            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, xMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
            playerYaw = BitConverter.ToSingle(buffer, 0);

            directionLabel.Text = playerYaw.ToString();

            byte[] binNewYaw = BitConverter.GetBytes(yawAngle - readWriteXMouseDiff);
            ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, writableXMouseAddr, binNewYaw, (uint)binNewYaw.Length, out bytesRead);

            //aimbot code for vertical plane
            ProcessMemoryReaderApi.ReadProcessMemory(process.Handle, yMouseAddr, buffer, (uint)buffer.Length, out bytesRead);
            playerPitch = BitConverter.ToSingle(buffer, 0);

            byte[] binNewPitch = BitConverter.GetBytes(pitchAngle + 2 - readWriteYMouseDiff);
            ProcessMemoryReaderApi.WriteProcessMemory(process.Handle, writableYMouseAddr, binNewPitch, (uint)binNewPitch.Length, out bytesRead);
        }
    }
}
