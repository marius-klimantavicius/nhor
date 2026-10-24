using System;

namespace Marius.Winter.Playground.Scenarios;

// Authored 32px preview-pack fixtures; two raster frames and a lean moving Lottie shape.
internal static class OperationFeedbackMedia
{
    public static readonly byte[] Raster = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAQElEQVR4nGMUN/NmoCVgoqnpoxaMWkAVwILGv13wAZdS1QkCZFgw9INo1IJRC0YtGLVg1AJqAMbRhteoBYPfAgBlbQRx0AkEsQAAAABJRU5ErkJggg==");
    public static readonly byte[] Gif = Convert.FromBase64String("R0lGODlhIAAgAIEAAPKmOxc2SwAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQAGQAAACwAAAAAIAAgAAAIVQADCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaJFhAAyatR4caPHjh45WgwpsiLJjCBJpgy58uPIky03xixJ8SSAmShfqtTJkqdLkzAvCh1KtKjRo0iPBgQAIfkEABkAAAAsBQAIABcAEQCB8qY7FzZLAAAAAAAACEEAAwgcSJAggIMIERZcODChQ4YMHSaEuFCiQooGLQLAmNEix4YaPwrUuFEkSZEBTpoMudJjS4koVX6UyZEmRpIBAQA7");
    public const string Lottie = """
{"v":"5.7.4","fr":20,"ip":0,"op":40,"w":64,"h":32,"nm":"Preview pulse","ddd":0,"assets":[],"layers":[{"ddd":0,"ind":1,"ty":4,"nm":"Preview marker","sr":1,"ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},"p":{"a":1,"k":[{"t":0,"s":[12,16,0],"e":[52,16,0],"o":{"x":0.33,"y":0},"i":{"x":0.67,"y":1}},{"t":20,"s":[52,16,0],"e":[12,16,0],"o":{"x":0.33,"y":0},"i":{"x":0.67,"y":1}},{"t":40,"s":[12,16,0]}]},"a":{"a":0,"k":[0,0,0]},"s":{"a":0,"k":[100,100,100]}},"shapes":[{"ty":"rc","p":{"a":0,"k":[0,0]},"s":{"a":0,"k":[14,20]},"r":{"a":0,"k":3}},{"ty":"fl","c":{"a":0,"k":[0.95,0.65,0.23,1]},"o":{"a":0,"k":100},"r":1}],"ip":0,"op":40,"st":0,"bm":0}]}
""";
}
