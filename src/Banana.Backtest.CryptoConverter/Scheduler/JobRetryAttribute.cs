using System.ComponentModel;
using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.States;
using Hangfire.Storage;
using Newtonsoft.Json;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class JobRetryAttribute : JobFilterAttribute, IElectStateFilter, IApplyStateFilter
{
    /// <summary>
    /// Represents the default number of retry attempts. This field is read-only.
    /// </summary>
    /// <remarks>
    /// The value of this field is <c>10</c>.
    /// </remarks>
    public static readonly int DefaultRetryAttempts = 10;

    private static readonly Func<long, int> DefaultDelayInSecondsByAttemptFunc = attempt =>
    {
        var random = new Random();
        return (int)Math.Round(
            Math.Pow(attempt - 1, 4) + 15 + random.Next(30) * attempt);
    };

    private readonly ILog _logger = LogProvider.For<AutomaticRetryAttribute>();

    private readonly object _lockObject = new();
    private int _attempts;
    private int[]? _delaysInSeconds;
    private Func<long, int>? _delayInSecondsByAttemptFunc;
    private AttemptsExceededAction _onAttemptsExceeded;
    private bool _logEvents;
    private Type[]? _onlyOn;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomaticRetryAttribute"/>
    /// class with <see cref="DefaultRetryAttempts"/> number.
    /// </summary>
    public JobRetryAttribute()
    {
        Attempts = DefaultRetryAttempts;
        DelayInSecondsByAttemptFunc = DefaultDelayInSecondsByAttemptFunc;
        LogEvents = true;
        OnAttemptsExceeded = AttemptsExceededAction.Fail;
        Order = 20;
    }

    /// <summary>
    /// Gets or sets the maximum number of automatic retry attempts.
    /// </summary>
    /// <value>Any non-negative number.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value in a set operation is less than zero.</exception>
    public int Attempts
    {
        get
        {
            lock (_lockObject)
            {
                return _attempts;
            }
        }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    @"Attempts value must be equal or greater than zero.");
            }

            lock (_lockObject)
            {
                _attempts = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the delays between attempts.
    /// </summary>
    /// <value>An array of non-negative numbers.</value>
    /// <exception cref="ArgumentNullException">The value in a set operation is null.</exception>
    /// <exception cref="ArgumentException">The value contain one or more negative numbers.</exception>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int[]? DelaysInSeconds
    {
        get
        {
            lock (_lockObject)
            {
                return _delaysInSeconds;
            }
        }
        set
        {
            if (value != null)
            {
                if (value.Length == 0)
                    throw new ArgumentNullException(nameof(value));
                if (value.Any(static delay => delay < 0))
                    throw new ArgumentException(
                        $"{nameof(DelaysInSeconds)} value must be an array of non-negative numbers.",
                        nameof(value));
            }

            lock (_lockObject)
            {
                _delaysInSeconds = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a function using to get a delay by an attempt number.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value in a set operation is null.</exception>
    [JsonIgnore]
    public Func<long, int>? DelayInSecondsByAttemptFunc
    {
        get
        {
            lock (_lockObject)
            {
                return _delayInSecondsByAttemptFunc;
            }
        }
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            lock (_lockObject)
            {
                _delayInSecondsByAttemptFunc = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a candidate state for a background job that
    /// will be chosen when number of retry attempts exceeded.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public AttemptsExceededAction OnAttemptsExceeded
    {
        get
        {
            lock (_lockObject)
            {
                return _onAttemptsExceeded;
            }
        }
        set
        {
            lock (_lockObject)
            {
                _onAttemptsExceeded = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to produce log messages on retry attempts.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    [DefaultValue(true)]
    public bool LogEvents
    {
        get
        {
            lock (_lockObject)
            {
                return _logEvents;
            }
        }
        set
        {
            lock (_lockObject)
            {
                _logEvents = value;
            }
        }
    }

    /// <summary>
    /// Gets a sets an array of exception types that will be used to determine whether
    /// automatic retry logic should be attempted to run. By default, it will be run on
    /// any exception, but this property allow to reduce it only to some specific
    /// exception types and their subtypes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Type[]? OnlyOn
    {
        get
        {
            lock (_lockObject)
            {
                return _onlyOn;
            }
        }
        set
        {
            lock (_lockObject)
            {
                _onlyOn = value;
            }
        }
    }

    /// <inheritdoc />
    public void OnStateElection(ElectStateContext context)
    {
        var failedState = context.CandidateState as FailedState;
        if (failedState == null)
        {
            // This filter accepts only failed job state.
            return;
        }

        if (_onlyOn != null && _onlyOn.Length > 0)
        {
            var exceptionType = failedState.Exception.GetType();
            var satisfied = false;

            foreach (var onlyOn in _onlyOn)
            {
                if (onlyOn.GetTypeInfo().IsAssignableFrom(exceptionType.GetTypeInfo()))
                {
                    satisfied = true;
                    break;
                }
            }

            if (!satisfied)
                return;
        }

        var retryAttempt = context.GetJobParameter<int>("RetryCount", allowStale: true) + 1;

        if (retryAttempt <= Attempts)
        {
            ScheduleAgainLater(context, retryAttempt, failedState);
        }
        else if (retryAttempt > Attempts && OnAttemptsExceeded == AttemptsExceededAction.Delete)
        {
            TransitionToDeleted(context, failedState);
        }
        else
        {
            if (LogEvents)
            {
                _logger.ErrorException(
                    $"Failed to process the job '{context.BackgroundJob.Id}': an exception occurred.",
                    failedState.Exception);
            }
        }
    }

    /// <inheritdoc />
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is ScheduledState &&
            context.NewState.Reason != null &&
            context.NewState.Reason.StartsWith("Retry attempt", StringComparison.OrdinalIgnoreCase))
        {
            transaction.AddToSet("retries", context.BackgroundJob.Id);
        }
    }

    /// <inheritdoc />
    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.OldStateName == ScheduledState.StateName)
        {
            transaction.RemoveFromSet("retries", context.BackgroundJob.Id);
        }
    }

    /// <summary>
    /// Schedules the job to run again later. See <see cref="DelayInSecondsByAttemptFunc"/>.
    /// </summary>
    /// <param name="context">The state context.</param>
    /// <param name="retryAttempt">The count of retry attempts made so far.</param>
    /// <param name="failedState">Object which contains details about the current failed state.</param>
    private void ScheduleAgainLater(ElectStateContext context, int retryAttempt, FailedState failedState)
    {
        context.SetJobParameter("RetryCount", retryAttempt);

        int delayInSeconds;

        if (_delaysInSeconds != null)
        {
            delayInSeconds = retryAttempt <= _delaysInSeconds.Length
                ? _delaysInSeconds[retryAttempt - 1]
                : _delaysInSeconds.Last();
        }
        else
        {
            delayInSeconds = DelayInSecondsByAttemptFunc?.Invoke(retryAttempt) ?? 1;
        }

        var delay = TimeSpan.FromSeconds(delayInSeconds);

        const int maxMessageLength = 50;
        var exceptionMessage = failedState.Exception.Message.Length > maxMessageLength
            ? failedState.Exception.Message[..(maxMessageLength - 1)] + "…"
            : failedState.Exception.Message;

        // If attempt number is less than max attempts, we should
        // schedule the job to run again later.

        var reason = $"Retry attempt {retryAttempt} of {Attempts}: {exceptionMessage}";

        context.CandidateState = delay == TimeSpan.Zero
            ? new EnqueuedState { Reason = reason, Queue = context.BackgroundJob.Job.Queue }
            : new ScheduledState(delay) { Reason = reason };

        if (LogEvents)
        {
            _logger.WarnException(
                $"Failed to process the job '{context.BackgroundJob.Id}': an exception occurred. Retry attempt {retryAttempt} of {Attempts} will be performed in {delay}.",
                failedState.Exception);
        }
    }

    /// <summary>
    /// Transition the candidate state to the deleted state.
    /// </summary>
    /// <param name="context">The state context.</param>
    /// <param name="failedState">Object which contains details about the current failed state.</param>
    private void TransitionToDeleted(ElectStateContext context, FailedState failedState)
    {
        context.CandidateState = new DeletedState(new ExceptionInfo(failedState.Exception))
        {
            Reason = Attempts > 0
                ? "Exceeded the maximum number of retry attempts."
                : "Retries were disabled for this job."
        };

        if (LogEvents)
        {
            _logger.WarnException(
                $"Failed to process the job '{context.BackgroundJob.Id}': an exception occured. Job was automatically deleted because the retry attempt count exceeded {Attempts}.",
                failedState.Exception);
        }
    }
}
