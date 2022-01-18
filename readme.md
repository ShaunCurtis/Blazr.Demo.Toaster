## Introduction

This article shows how to build a simple Bootstrap Toaster in Blazor.

It demonstrates several programming principles and coding patterns that are applicable in almost all Blazor applications.

1. **Separation of Concerns** - Data doesn't belong in the UI.  The Toaster UI component contains no data or data management.  It job is to display toasts.

2. **The Blazor Show/Hide Pattern** - I was reluctant to call this a pattern, but the number of times I've see programmers trying to achieve this with JSInterop changed my mind.  This pattern implements CSS framework `.Show()` and `.Hide()` Javascript functionality in C# in the component.

3. **The Blazor Notification Pattern** - decouples UI components from the underlying data that drive their behaviour using events.

4. **Value Objects** - Modern design emphasises the use of value objects wherever appropriate.

## Repo and Demo Site

You can find the code in my [Blazr.Demo.Toaster Repo](https://github.com/ShaunCurtis/Blazr.Demo.Toaster).

A demo site can be found here at [https://blazr-demo-database-server.azurewebsites.net](https://blazr-demo-database-server.azurewebsites.net)

![Example](https://shauncurtis.github.io/posts/assets/Toaster/Toaster-Startup.png)

## Code Classes

### Toast

First an `enum` for the message colour.  It uses Bootstrap nomenclature directly to make building Css strings simple.

```csharp
public enum MessageColour
{
    Primary, Secondary, Dark, Light, Success, Danger, Warning, Info
}
```
`Toast` is declared as a value object.  Once we've created an instance, we have no reason to change it.

1. Toast is declared as a `record`.
2. There are five public properties that are used by the UI to display the toast. All are declared as immutable  with `{ get; init; }`.
3. `TimeToBurn` uses `DateTimeOffset` to give timezone independant absolute time. 

```csharp
public record Toast
{
    public Guid Id = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public MessageColour MessageColour { get; init; } = MessageColour.Primary;
    public DateTimeOffset TimeToBurn { get; init; } = DateTimeOffset.Now.AddSeconds(30);
```
Next:

1. `Posted` is a object creation timestamp.
2. `IsBurnt` is a bool to check toast expiration.
3. `elapsedTime` is used to build `ElapsedTimeText`.
4. `ElapsedTimeText` is used by the UI Component.

```csharp
public readonly DateTimeOffset Posted = DateTimeOffset.Now;
public bool IsBurnt => TimeToBurn < DateTimeOffset.Now;
private TimeSpan elapsedTime => Posted - DateTimeOffset.Now;

public string ElapsedTimeText =>
    elapsedTime.Seconds > 60
    ? $"posted {-elapsedTime.Minutes} mins ago"
    : $"posted {-elapsedTime.Seconds} secs ago";
```
Finally a static constructor helper method.

```csharp
public static Toast NewToast(string title, string message, MessageColour messageColour, int secsToLive)
    => new Toast
    {
        Title = title,
        Message = message,
        MessageColour = messageColour,
        TimeToBurn = DateTimeOffset.Now.AddSeconds(secsToLive)
    };
```

### Toaster Service

`ToasterService` is a Dependancy Injection service that holds and manages Toasts.  It has a private list to hold the toasts with add and clear methods.  There's a timer to trigger `ClearBurntToast` to clear out expired toasts and if necessary raise the `ToasterChanged` event.  It also raises the `ToasterTimerElapsed` event on each timer cycle.

1. It's a standard class.
2. Implements `IDisposable` as it registers an event handler with the timer that needs disposing correctly.
3. Has a read only private collection of `Toast` instances.  The list is managed internally.
4. Has an internal timer that drives the classic service behaviour.
   
```csharp
public class ToasterService : IDisposable
{
    private readonly List<Toast> _toastList = new List<Toast>();
    private System.Timers.Timer _timer = new System.Timers.Timer();
```

There are two public events that other services or UI components can subscribe to.

1.  `ToasterChanged` is raised whenever the toast list is changed.
2.  `ToasterTimerElapsed` is raised on each timer loop.
3.  `HasToasts` is a simple status bool.

```csharp

    public event EventHandler? ToasterChanged;
    public event EventHandler? ToasterTimerElapsed;
    public bool HasToasts => _toastList.Count > 0;
```
`ClearBurntToast` is our toast list management method.  It checks to see it there's any burnt toast.  It there is it clears them out and raises the `ToasterChanged` event.

```csharp
    private bool ClearBurntToast()
    {
        var toastsToDelete = _toastList.Where(item => item.IsBurnt).ToList();
        if (toastsToDelete is not null && toastsToDelete.Count > 0)
        {
            toastsToDelete.ForEach(toast => _toastList.Remove(toast));
            this.ToasterChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }
```
`TimerElapsed` is our event handler for the timer elapsed event.  It clears any burnt toast and raises the `ToasterTimerElapsed` event. 

```csharp
    private void TimerElapsed(object? sender, ElapsedEventArgs e)
    { 
        this.ClearBurntToast();
        this.ToasterTimerElapsed?.Invoke(this, EventArgs.Empty);
    }
```
The constructor adds a welcome `Toast`, sets up the timer and registers the event handler.

```csharp
    public ToasterService()
    {
        AddToast(new Toast { Title = "Welcome Toast", Message = "Welcome to this Application.  I'll disappear after 15 seconds.", TTD = DateTimeOffset.Now.AddSeconds(10) });
        _timer.Interval = 5000;
        _timer.AutoReset = true;
        _timer.Elapsed += this.TimerElapsed;
        _timer.Start();
    }
```
The *CRUD* type operations are self explanatory. Each calls `ClearBurntToast` to run the management method. 

```csharp

    public List<Toast> GetToasts()
    {
        ClearBurntToast();
        return _toastList;
    }

    public void AddToast(Toast toast)
    {
        _toastList.Add(toast);
        // only raise the ToasterChanged event if it hasn't already been raised by ClearBurntToast
        if (!this.ClearBurntToast())
            this.ToasterChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearToast(Toast toast)
    {
        if (_toastList.Contains(toast))
        {
            _toastList.Remove(toast);
            // only raise the ToasterChanged event if it hasn't already been raised by ClearBurntToast
            if (!this.ClearBurntToast())
                this.ToasterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
```
Finally the `Dispose` method clears the timer event handler. 

```csharp
    public void Dispose()
    {
        if (_timer is not null)
        {
            _timer.Elapsed += this.TimerElapsed;
            _timer.Stop();
        }
    }
}
```

`ToasterService` can run as either a `Scoped` or `Singleton` service, depending on what you're want it to do.

### Toaster

`Toaster` is the UI component.

The razor markup implements the Bootstrap Toast markup, with a `foreach` loop to add each toast.  The markup will display the toasts stacked in the top right.

1. The component checks if there's anything to display - `this.toasterService.HasToasts`.  If not then no content is rendered -  the Blazor Show/Hide Pattern.
2. `@_toastCss` gets the correct Css string for the message colour.
3. The close **X** calls `this.ClearToast(toast)` to remove a toast.

```csharp
@implements IDisposable
@if (this.toasterService.HasToasts)
{
    <div class="">
        <div class="toast-container position-absolute top-0 end-0 mt-5 pt-5 pe-2">
            @foreach (var toast in this.toasterService.GetToasts())
            {
                var _toastCss = toastCss(toast);
                <div class="toast show" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="toast-header @_toastCss">
                        <strong class="me-auto">@toast.Title</strong>
                        <small class="@_toastCss">@toast.ElapsedTimeText</small>
                        <button type="button" class="btn-close btn-close-white" aria-label="Close" @onclick="() => this.ClearToast(toast)"></button>
                    </div>
                    <div class="toast-body">
                        @toast.Message
                    </div>
                </div>
            }
        </div>
    </div>
}
```

The code behind class:

1. Inherits from `ComponentBase` and implements `IDisposable` to unhook event handlers.
2. Injects the `ToasterService`.
3. Creates a second null forgiving reference to the `ToasterService`.  We're in the nullable world, but the C# compiler doesn't know `ToasterService` can't be null, so we create a second null forgiving reference so we don't need to null forgive ever time we use the `ToasterService` instance.
   
```csharp
public partial class Toaster : ComponentBase, IDisposable
{
    [Inject] private ToasterService? _toasterService { get; set; }

    private ToasterService toasterService => _toasterService!;
```

`ToastChanged` is the event handler for the `ToasterService` events.  It invokes `StateHasChanged` on the UI thread.

```csharp
    private void ToastChanged(object? sender, EventArgs e)
        => this.InvokeAsync(this.StateHasChanged);
```

`OnInitialized` registers the two `ToasterService` events to `ToastChanged`, and `Dispose` removes them.

```csharp
    protected override void OnInitialized()
    { 
        this.toasterService.ToasterChanged += ToastChanged;
        this.toasterService.ToasterTimerElapsed += ToastChanged;
    }

    public void Dispose()
    { 
        this.toasterService.ToasterChanged -= ToastChanged;
        this.toasterService.ToasterTimerElapsed -= ToastChanged;
    }
```

Finally `ClearToast` clears the selected toast from the service and `toastCss` gets the toast background colour.

```csharp
    private void ClearToast(Toast toast)
        => toasterService.ClearToast(toast);

    private string toastCss(Toast toast)
    {
        var colour = Enum.GetName(typeof(MessageColour), toast.MessageColour)?.ToLower();
        return toast.MessageColour switch
        {
            MessageColour.Light => "bg-light",
            _ => $"bg-{colour} text-white"
        };
    }
}
```

## Implementing

1. Add `ToasterService` to the DI service container in Program.

2. Add the component to either `Layout` or `App` or wherever you wish to use it.

```xml
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly" PreferExactMatches="@true">
    ....
    </Router>
</CascadingAuthenticationState>
<Toaster />
```

3. Inject the service into any pages where you want to raise toasts, and call `AddToast`.  The example below shows a demo `Index` page.

```csharp
@page "/"

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<SurveyPrompt Title="How is Blazor working for you?" />
<div class="m-2 p-2">
    <button class="btn btn-primary" @onclick="AddToast" >Add a Toast</button>
</div>

@code {
    [Inject] private ToasterService? _toasterService {get; set;}
    
    private ToasterService toasterService => _toasterService!;

    private void AddToast()
    => toasterService.AddToast(Toast.NewToast("Hello World", "Hello from Blazor", MessageColour.Info, 30));
}
```

## Wrap Up

The design demonstrates a clean separation of data from UI.  All data handling happens in `ToasterService`.  `Toaster` uses references to the data objects in `ToasterService`.  

The Blazor notification pattern is used to update the UI whenever the toast list changes.  `Toaster` registers an event handler with the two `ToasterService` events which re-renders the component whenever an event occurs.

`Toaster` demonstrates how to show and hide UI markup based on state.

`Toast` is a value object.  It simplifies equality checking (we don't do any here) and ensures toasts can't be modified once created.