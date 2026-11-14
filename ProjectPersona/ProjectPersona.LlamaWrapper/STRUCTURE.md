# ProjectPersona.LlamaWrapper

Parent: [project index](../../docs/project-structure.md)

Class library, `net10.0`. Wrapper over the native `llama.dll` of llama.cpp build **b12359** (Windows x64): GGUF model loading, embedding context, tokenization. No dependencies on other projects.

The ABI is tied to b12359 (CPU/Vulkan/CUDA builds of that release). Other builds may be incompatible because `llama_context_params` differs.

```
ProjectPersona.LlamaWrapper/
├── LlamaModelSession.cs    full model + context, Embed with L2 normalization
├── LlamaLoadOptions.cs     n_ctx, n_batch, n_ubatch, n_seq_max, n_threads, n_threads_batch, n_gpu_layers, devices, flash_attn; null keeps the llama.dll default
├── LlamaTokenizer.cs       CountTokens / Tokenize (Attach to an already loaded model)
├── LlamaTokenPiece.cs      token id and text piece
├── LlamaEmbeddingMath.cs   L2 normalization of vectors
└── Native/LlamaNative.cs   P/Invoke: model, context, batch, decode, embeddings
```

## Notes

- The directory containing `llama.dll` is set only through `LlamaSettings.NativeLibraryDirectory`. There is no auto-discovery (no environment variable, application directory or neighbouring assemblies); an empty value is an error.
- The model file is `LlamaSettings.ModelPath` (the embedding model used is `embeddinggemma-2-Q8_0.gguf`, supplied locally).
- `LlamaModelSessionHolder` (in RagTextSearch) keeps one session per process; `LlamaTextTokenizer` and `LlamaTextEmbedder` share it.

## Related

- Consumers and settings: [RagTextSearch](../ProjectPersona.RagTextSearch/STRUCTURE.md)
