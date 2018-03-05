﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace BloodDonationCoin.Core
{
    /// <summary>
    /// Base class for wrappers around command line programs.
    /// </summary>
    public abstract class BaseWrapper
    {
        protected string WalletPath { get; set; }
        protected string ExeFileName { get; set; }
        protected Process WrapperProcess { get; set; }
        protected bool HandleLines { get; set; }

        public EventHandler<WrapperEvent<LogLine>> OutputReceived;
        public EventHandler<WrapperErrorEvent> Error;
        public EventHandler<WrapperEvent<string>> Information;
        public EventHandler<WrapperStatusEvent> StatusChanged;

        /// <summary>
        /// Gets the executable path. Returns the path if the file exists, 
        /// will return the path without extension otherwhise (used for linux).
        /// </summary>
        protected string ExecutablePath
        {
            get
            {
                string path = System.IO.Path.Combine(Path.GetDirectoryName(WalletPath), ExeFileName);
                if (File.Exists(path) || !Path.HasExtension(path))
                {
                    return path;
                }
                else 
                {
                    return Path.ChangeExtension(path, null);
                }
            }
        }

        public BaseWrapper(string walletPath, string exeFileName)
        {
            HandleLines = true;
            WalletPath = walletPath;
            ExeFileName = exeFileName;
        }

        /// <summary>
        /// Stop the running process.
        /// </summary>
        public virtual void Exit()
        {
            HandleLines = false;

            if (WrapperProcess != null && !WrapperProcess.HasExited)
            {
                WriteLine("exit");

                WrapperProcess.WaitForExit(60000);

                if (!WrapperProcess.HasExited)
                {
                    WrapperProcess.Kill();
                    WrapperProcess.WaitForExit();
                }

                WrapperProcess.Close();
            }
        }

        /// <summary>
        /// Write a line to the standard input.
        /// </summary>
        /// <param name="line"></param>
        public void WriteLine(string line)
        {
            WrapperProcess.StandardInput.WriteLine(line);
        }

        /// <summary>
        /// Check if we can find the executable to start.
        /// </summary>
        /// <returns></returns>
        protected bool CanStart()
        {
            if (!File.Exists(ExecutablePath))
            {
                SendError(
                    string.Format(
                        "The {0} executable for the daemon could not be found. You should put CryptoNote Wallet in the same directory as the {0} executable.",
                        ExeFileName),
                    true);
                return false;
            }

            return true;
        }

        protected void SendError(string message, bool shouldExit)
        {
            if (Error != null)
            {
                Error.Invoke(this, new WrapperErrorEvent(message, shouldExit));
            }
        }

        protected void SendInformation(string message)
        {
            if (Information != null)
            {
                Information.Invoke(this, new WrapperEvent<string>(message));
            }
        }

        protected void UpdateStatus(WalletStatus status, string message)
        {
            if (StatusChanged != null)
            {
                StatusChanged.Invoke(this, new WrapperStatusEvent(status, message));
            }
        }

        /// <summary>
        /// Read the next line from the streamreader. Handle the output.
        /// </summary>
        /// <param name="isError">Should we read from the StandardError instead of StandardOutput?</param>
        protected async void ReadNextLine(bool isError)
        {
            if (HandleLines)
            {
                StreamReader reader = isError ? WrapperProcess.StandardError : WrapperProcess.StandardOutput;

                string line = await reader.ReadLineAsync();
                HandleLine(line ?? string.Empty, isError);
            }
        }

        /// <summary>
        /// Interpret the current wallet output and call relevant event listeners.
        /// </summary>
        /// <param name="line">Current line.</param>
        /// <param name="isError">Is the line read from StandardError?</param>
        /// <param name="lineIsHandled">Has the line been handled?</param>
        protected virtual void HandleLine(string line, bool isError, bool lineIsHandled = true)
        {
            if (OutputReceived != null)
            {
                OutputReceived.Invoke(this, new WrapperEvent<LogLine>(new LogLine(line, lineIsHandled)));
            }

            ReadNextLine(isError);
        }
    }
}
