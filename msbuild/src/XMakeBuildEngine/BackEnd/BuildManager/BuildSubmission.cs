﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implementation of the Build Submission.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using System.Globalization;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// A callback used to receive notification that a build has completed.
    /// </summary>
    /// <remarks>
    /// When this delegate is invoked, the WaitHandle on the BuildSubmission will have been be signalled and the OverallBuildResult will be valid.
    /// </remarks>
    public delegate void BuildSubmissionCompleteCallback(BuildSubmission submission);

    /// <summary>
    /// A BuildSubmission represents an build request which has been submitted to the BuildManager for processing.  It may be used to
    /// execute synchronous or asynchronous build requests and provides access to the results upon completion.
    /// </summary>
    /// <remarks>
    /// This class is thread-safe.
    /// </remarks>
    public class BuildSubmission
    {
        /// <summary>
        /// The callback to invoke when the submission is complete.
        /// </summary>
        private BuildSubmissionCompleteCallback _completionCallback;

        /// <summary>
        /// The completion event.
        /// </summary>
        private ManualResetEvent _completionEvent;

        /// <summary>
        /// Flag indicating if logging is done.
        /// </summary>
        private bool _loggingCompleted;

        /// <summary>
        /// True if it has been invoked
        /// </summary>
        private int _completionInvoked;

        /// <summary>
        /// The results of the build.
        /// </summary>
        private BuildResult _buildResult;

        /// <summary>
        /// Flag indicating whether synchronous wait should support legacy threading semantics.
        /// </summary>
        private bool _legacyThreadingSemantics;

        /// <summary>
        /// Constructor
        /// </summary>
        internal BuildSubmission(BuildManager buildManager, int submissionId, BuildRequestData requestData, bool legacyThreadingSemantics)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildManager, "buildManager");
            ErrorUtilities.VerifyThrowArgumentNull(requestData, "requestData");

            BuildManager = buildManager;
            SubmissionId = submissionId;
            BuildRequestData = requestData;
            _completionEvent = new ManualResetEvent(false);
            _loggingCompleted = false;
            _completionInvoked = 0;
            _legacyThreadingSemantics = legacyThreadingSemantics;
        }

        /// <summary>
        /// The BuildManager with which this submission is associated.
        /// </summary>
        public BuildManager BuildManager
        {
            get;
            private set;
        }

        /// <summary>
        /// An ID uniquely identifying this request from among other submissions within the same build.
        /// </summary>
        public int SubmissionId
        {
            get;
            private set;
        }

        /// <summary>
        /// The asynchronous context provided to <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/>, if any.
        /// </summary>
        public Object AsyncContext
        {
            get;
            private set;
        }

        /// <summary>
        /// A <see cref="System.Threading.WaitHandle"/> which will be signalled when the build is complete.  Valid after <see cref="BuildSubmission.Execute()"/> or <see cref="BuildSubmission.ExecuteAsync(BuildSubmissionCompleteCallback, object)"/> returns, otherwise null.
        /// </summary>
        public WaitHandle WaitHandle
        {
            get
            {
                return _completionEvent;
            }
        }

        /// <summary>
        /// Returns true if this submission is complete.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                return WaitHandle.WaitOne(new TimeSpan(0));
            }
        }

        /// <summary>
        /// The result of the build.  Valid only after WaitHandle has become signalled.
        /// </summary>
        public BuildResult BuildResult
        {
            get
            {
                return _buildResult;
            }

            set
            {
                _buildResult = value;
            }
        }

        /// <summary>
        /// The BuildRequestData being used for this submission.
        /// </summary>
        internal BuildRequestData BuildRequestData
        {
            get;
            private set;
        }

        /// <summary>
        /// The build request for execution.
        /// </summary>
        internal BuildRequest BuildRequest
        {
            get;
            set;
        }

        /// <summary>
        /// Starts the request and blocks until results are available.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public BuildResult Execute()
        {
            LegacyThreadingData legacyThreadingData = ((IBuildComponentHost)BuildManager).LegacyThreadingData;
            legacyThreadingData.RegisterSubmissionForLegacyThread(this.SubmissionId);

            ExecuteAsync(null, null, _legacyThreadingSemantics);
            if (_legacyThreadingSemantics)
            {
                RequestBuilder.WaitWithBuilderThreadStart(new WaitHandle[] { WaitHandle }, false, legacyThreadingData, this.SubmissionId);
            }
            else
            {
                WaitHandle.WaitOne();
            }

            legacyThreadingData.UnregisterSubmissionForLegacyThread(this.SubmissionId);

            return BuildResult;
        }

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        public void ExecuteAsync(BuildSubmissionCompleteCallback callback, object context)
        {
            ExecuteAsync(callback, context, false);
        }

        /// <summary>
        /// Sets the event signaling that the build is complete.
        /// </summary>
        internal void CompleteResults(BuildResult result)
        {
            ErrorUtilities.VerifyThrowArgumentNull(result, "result");

            // We verify that we got results from the same configuration, but not necessarily the same request, because we are 
            // rather flexible in how users are allowed to submit multiple requests for the same configuration.  In this case, the
            // request id of the result will match the first request, even though it will contain results for all requests (including
            // this one.)
            ErrorUtilities.VerifyThrow(result.ConfigurationId == BuildRequest.ConfigurationId, "BuildResult doesn't match BuildRequest configuration");

            if (BuildResult == null)
            {
                BuildResult = result;
            }

            CheckForCompletion();
        }

        /// <summary>
        /// Indicates that all logging events for this submission are complete.
        /// </summary>
        internal void CompleteLogging(bool waitForLoggingThread)
        {
            if (waitForLoggingThread)
            {
                ((Microsoft.Build.BackEnd.Logging.LoggingService)((IBuildComponentHost)BuildManager).LoggingService).WaitForThreadToProcessEvents();
            }

            _loggingCompleted = true;
            CheckForCompletion();
        }

        /// <summary>
        /// Starts the request asynchronously and immediately returns control to the caller.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The request has already been started or is already complete.</exception>
        private void ExecuteAsync(BuildSubmissionCompleteCallback callback, object context, bool allowMainThreadBuild)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!IsCompleted, "SubmissionAlreadyComplete");
            _completionCallback = callback;
            AsyncContext = context;
            BuildManager.ExecuteSubmission(this, allowMainThreadBuild);
        }

        /// <summary>
        /// Determines if we are completely done with this submission and can complete it so the user may access results.
        /// </summary>
        private void CheckForCompletion()
        {
            if (BuildResult != null && _loggingCompleted)
            {
                bool hasCompleted = (Interlocked.Exchange(ref _completionInvoked, 1) == 1);
                if (!hasCompleted)
                {
                    _completionEvent.Set();

                    if (null != _completionCallback)
                    {
                        WaitCallback callback = new WaitCallback
                        (
                         delegate (object state)
                         {
                             _completionCallback(this);
                         });

                        ThreadPoolExtensions.QueueThreadPoolWorkItemWithCulture(callback, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture);
                    }
                }
            }
        }
    }
}
