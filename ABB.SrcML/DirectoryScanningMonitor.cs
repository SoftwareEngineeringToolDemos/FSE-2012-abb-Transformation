﻿/******************************************************************************
 * Copyright (c) 2013 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - Initial implementation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;
using Timer = System.Timers.Timer;

namespace ABB.SrcML {

    /// <summary>
    /// The directory scanning monitor scans a collection of directories every
    /// <see cref="ScanInterval" /> seconds for source changes and updates the appropriate
    /// <see cref="IArchive">archives</see>. <para>The directory scanning monitor lets you
    /// periodically scan a set of folders and </para>
    /// </summary>
    /// <remarks>
    /// The directory scanning monitor uses a <see cref="System.Timers.Timer"/> to periodically scan
    /// each directory in <see cref="MonitoredDirectories"/>. It first examines all of the archived
    /// files to identify files that have been deleted. Next, it checks the files
    /// </remarks>
    public class DirectoryScanningMonitor : AbstractFileMonitor {
        private const int IDLE = 0;
        private const int RUNNING = 1;
        private const int STOPPED = -1;
        private List<string> folders;
        private Timer ScanTimer;
        private int syncPoint;

        /// <summary>
        /// Create a new directory scanning monitor
        /// </summary>
        /// <param name="foldersToMonitor">An initial list of
        /// <see cref="MonitoredDirectories">folders to /see></param>
        /// <param name="scanInterval">The <see cref="ScanInterval"/> in seconds</param>
        /// <param name="baseDirectory">The base directory to use for the archives of this
        /// monitor</param>
        /// <param name="defaultArchive">The default archive to use</param>
        /// <param name="otherArchives">Other archives for specific extensions</param>
        public DirectoryScanningMonitor(ICollection<string> foldersToMonitor, double scanInterval, string baseDirectory, IArchive defaultArchive, params IArchive[] otherArchives)
            : base(baseDirectory, defaultArchive, otherArchives) {
            folders = new List<string>(foldersToMonitor.Count);
            folders.AddRange(foldersToMonitor);
            MonitoredDirectories = new ReadOnlyCollection<string>(folders);
            ScanTimer = new Timer();
            ScanInterval = scanInterval;
            ScanTimer.AutoReset = true;
            ScanTimer.Elapsed += ScanTimer_Elapsed;
            syncPoint = STOPPED;
        }

        /// <summary>
        /// Create a new directory scanning monitor
        /// </summary>
        /// <param name="baseDirectory">The base directory to use for the archives of this
        /// monitor</param>
        /// <param name="defaultArchive">The default archive to use</param>
        /// <param name="otherArchives">Other archives for specific extensions</param>
        public DirectoryScanningMonitor(string baseDirectory, IArchive defaultArchive, params IArchive[] otherArchives)
            : this(new List<string>(), baseDirectory, defaultArchive, otherArchives) { }

        /// <summary>
        /// Create a new directory scanning monitor
        /// </summary>
        /// <param name="foldersToMonitor">An initial list of
        /// <see cref="MonitoredDirectories">folders to /see></param>
        /// <param name="baseDirectory">The base directory to use for the archives of this
        /// monitor</param>
        /// <param name="defaultArchive">The default archive to use</param>
        /// <param name="otherArchives">Other archives for specific extensions</param>
        public DirectoryScanningMonitor(ICollection<string> foldersToMonitor, string baseDirectory, IArchive defaultArchive, params IArchive[] otherArchives)
            : this(foldersToMonitor, 60, baseDirectory, defaultArchive, otherArchives) { }

        /// <summary>
        /// A read only collection of the directories being monitored. <para>In order to add a
        /// directory, use the <see cref="AddDirectory(string)"/>.</para>
        /// </summary>
        public ReadOnlyCollection<string> MonitoredDirectories { get; private set; }

        /// <summary>
        /// The scan interval is the number of seconds between each scan. By default it is set to
        /// 60.
        /// </summary>
        public double ScanInterval {
            get { return ScanTimer.Interval / 1000; }
            set { ScanTimer.Interval = value * 1000; }
        }

        /// <summary>
        /// Add a folder to <see cref="MonitoredDirectories"/>
        /// </summary>
        /// <param name="directoryPath">The directory to start monitoring</param>
        public void AddDirectory(string directoryPath) {
            folders.Add(directoryPath);
            if(ScanTimer.Enabled) {
                foreach(var fileName in EnumerateDirectory(directoryPath)) {
                    UpdateFile(fileName);
                }
            }
        }

        public IEnumerable<string> EnumerateDirectory(string directory) {
            var files = from filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                        select Path.GetFullPath(filePath);
            return files;
        }

        /// <summary>
        /// Returns an enumerable
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<string> EnumerateMonitoredFiles() {
            var monitoredFiles = from directory in MonitoredDirectories
                                 from fileName in EnumerateDirectory(directory)
                                 select fileName;
            return monitoredFiles;
        }

        /// <summary>
        /// Start scanning <see cref="MonitoredDirectories">monitored directories</see> every
        /// <see cref="ScanInterval"/> seconds.
        /// </summary>
        /// <remarks>
        /// Has no effect if the monitor is already running.
        /// </remarks>
        public override void StartMonitoring() {
            if(STOPPED == Interlocked.CompareExchange(ref syncPoint, IDLE, STOPPED)) {
                ScanTimer.Start();
            }
        }

        /// <summary>
        /// Stop monitoring <see cref="MonitoredDirectories">monitored directories</see>.
        /// </summary>
        /// <remarks>
        /// Stops monitoring
        /// </remarks>
        public override void StopMonitoring() {
            ScanTimer.Stop();

            while(Interlocked.CompareExchange(ref syncPoint, STOPPED, IDLE) != IDLE) {
                Thread.Sleep(1);
            }
            base.StopMonitoring();
        }

        /// <summary>
        /// Runs whenever the built-in timer expires.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// This function executes if it is not already running (from a previous event) and
        /// <see cref="StopMonitoring()"/> hasn't been called.
        /// </remarks>
        private void ScanTimer_Elapsed(object sender, ElapsedEventArgs e) {
            int sync = Interlocked.CompareExchange(ref syncPoint, RUNNING, IDLE);
            if(IDLE == sync) {
                UpdateArchives();
                syncPoint = IDLE;
            }
        }
    }
}