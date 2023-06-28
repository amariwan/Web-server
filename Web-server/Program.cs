using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});


var app = builder.Build();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseCors();

app.MapGet("/", () =>
{
    Console.WriteLine("Hello, fearless explorer!");
    return "Welcome to the magical world of code!";
});

app.MapGet("/{name:alpha}", (string name) =>
{
    if (name.ToLower() == "openai")
    {
        Console.WriteLine($"Hello, esteemed {name}! You're a wizard of AI!");
        return $"Hello, esteemed {name}! You're a wizard of AI!";
    }
    else
    {
        Console.WriteLine($"Hello, {name}! Did you bring your sense of humor today?");
        return $"Hello, {name}! Did you bring your sense of humor today?";
    }
});

app.MapGet("/hello/{name:alpha}", (string name) =>
{
    Console.WriteLine($"Hello, {name}! Let's have a dance-off with the robots!");
    return $"Hello, {name}! Let's have a dance-off with the robots!";
});

app.MapPost("/test/", () =>
{
    Console.WriteLine("Well, it was fun");
});



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.UseWebSockets(webSocketOptions);

app.Use(async (context, next) =>
{
    Console.WriteLine($"Path: {context.Request.Path}");
    if (context.Request.Path == "/wss")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocket(webSocket);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next();
    }
});


app.MapControllers();
async Task HandleWebSocket(WebSocket webSocket)
{
    var buffer = new byte[1024];
    WebSocketReceiveResult result;

    do
    {
        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var responseMessage = $"Received: {receivedMessage}";

            var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            break;
        }
    } while (!result.CloseStatus.HasValue);

    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
}

await app.RunAsync();