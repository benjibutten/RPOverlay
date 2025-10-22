using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace RPOverlay.Infra.Services;

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ChatService
{
    private ChatClient? _chatClient;
    private readonly List<OpenAI.Chat.ChatMessage> _conversationHistory = new();
    
    public bool IsConfigured => _chatClient != null;
    
    public void Configure(string apiKey, string? systemPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _chatClient = null;
            _conversationHistory.Clear();
            return;
        }
        
        try
        {
            _chatClient = new ChatClient("gpt-4o-mini", apiKey);
            _conversationHistory.Clear();
            
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                _conversationHistory.Add(new SystemChatMessage(systemPrompt));
            }
        }
        catch (Exception)
        {
            _chatClient = null;
            _conversationHistory.Clear();
            throw;
        }
    }
    
    public void ClearHistory()
    {
        var systemMessage = _conversationHistory.Count > 0 && 
                          _conversationHistory[0] is SystemChatMessage 
            ? _conversationHistory[0] 
            : null;
            
        _conversationHistory.Clear();
        
        if (systemMessage != null)
        {
            _conversationHistory.Add(systemMessage);
        }
    }
    
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            throw new InvalidOperationException("ChatService är inte konfigurerad. Ange API-nyckel i inställningar.");
        }
        
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            yield break;
        }
        
        // Add user message to history
        _conversationHistory.Add(new UserChatMessage(userMessage));
        
        var assistantMessageContent = string.Empty;
        Exception? streamException = null;
        
        // Stream the response
        await foreach (var update in _chatClient.CompleteChatStreamingAsync(
            _conversationHistory, 
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (update.ContentUpdate.Count > 0)
            {
                var textChunk = update.ContentUpdate[0].Text;
                assistantMessageContent += textChunk;
                yield return textChunk;
            }
        }
        
        // Add complete assistant response to history
        if (!string.IsNullOrEmpty(assistantMessageContent))
        {
            _conversationHistory.Add(new AssistantChatMessage(assistantMessageContent));
        }
        else if (streamException != null)
        {
            // Remove user message if failed
            if (_conversationHistory.Count > 0 && 
                _conversationHistory[^1] is UserChatMessage)
            {
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
            }
            
            throw new InvalidOperationException($"Fel vid kommunikation med OpenAI: {streamException.Message}", streamException);
        }
    }
}
