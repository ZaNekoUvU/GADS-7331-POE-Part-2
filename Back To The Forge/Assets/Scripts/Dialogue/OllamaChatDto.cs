using System;
using UnityEngine;

/// <summary>JSON DTOs for Ollama <c>/api/chat</c> (non-streaming).</summary>
[Serializable]
public class OllamaChatRequestDto
{
    public string model;
    public OllamaMessageDto[] messages;
    public bool stream;
    public OllamaOptionsDto options;
}

[Serializable]
public class OllamaMessageDto
{
    public string role;
    public string content;
    /// <summary>Some models (e.g. Qwen reasoning builds) may put text here when <see cref="content"/> is empty.</summary>
    public string thinking;
}

[Serializable]
public class OllamaOptionsDto
{
    public int num_predict;
    public float temperature;
}

[Serializable]
public class OllamaChatResponseDto
{
    public OllamaMessageDto message;
}

[Serializable]
public class OllamaErrorResponseDto
{
    public string error;
}
