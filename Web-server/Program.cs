using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;

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
app.MapControllers();
// Store WebSocket connections
var webSockets = new ConcurrentDictionary<string, WebSocket>();


app.Use(async (context, next) =>
{
    var currentEndpoint = context.GetEndpoint();

    if (currentEndpoint is null)
    {
        await next(context);
        return;
    }

    Console.WriteLine($"Endpoint: {currentEndpoint.DisplayName}");

    if (currentEndpoint is RouteEndpoint routeEndpoint)
    {
        Console.WriteLine($"  - Route Pattern: {routeEndpoint.RoutePattern}");
    }

    foreach (var endpointMetadata in currentEndpoint.Metadata)
    {
        Console.WriteLine($"  - Metadata: {endpointMetadata}");
    }

    await next(context);
});

app.MapGet("/", () =>
{
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
            var clientId = Guid.NewGuid().ToString();
            webSockets.TryAdd(clientId, webSocket);
            await HandleWebSocket(clientId);
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
async Task HandleWebSocket(string clientId)
{
    var webSocket = webSockets[clientId];

    var buffer = new byte[1024];
    WebSocketReceiveResult result;

    do
    {
        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received: {receivedMessage}");

            // Process the received message
            var httpClient = new HttpClient();
            var encodedMsg = Uri.EscapeDataString(receivedMessage);
            var apiKey = "sk-***************************************";
            var apiUrl = "https://api.openai.com/v1/chat/completions";

            var data = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = receivedMessage }
                }
            };

            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            try
            {
                var response = await httpClient.PostAsync(apiUrl, new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));
                var responseBody = await response.Content.ReadAsStringAsync();

                dynamic resultJson = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);

                // Get the assistant's reply
                var reply = resultJson.choices[0].message.content.ToString();
                Console.WriteLine("Assistant: " + reply);

                var responseBytes = Encoding.UTF8.GetBytes(reply);

                // Send the message only to the requesting client
                await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending request: {ex.Message}");
            }
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            webSockets.TryRemove(clientId, out _);
            break;
        }
    } while (!result.CloseStatus.HasValue);

    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
}


// Start reading from the console and send input to WebSocket clients
_ = Task.Run(async () =>
{
    while (true)
    {
        var input = Console.ReadLine();
        var responseBytes = Encoding.UTF8.GetBytes(input);

        foreach (var socket in webSockets.Values)
        {
            await socket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
});

await app.RunAsync();
