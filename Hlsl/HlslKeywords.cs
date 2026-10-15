using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Words a semantic can be but a name cannot (`uint o : OUT` makes no member `out`).
/// </summary>
public static class HlslKeywords
{
    private static readonly HashSet<string> Keywords =
    [
        "appendstructuredbuffer", "asm", "asm_fragment", "blendstate", "bool", "break",
        "buffer", "bytaddressbuffer", "byteaddressbuffer", "case", "cbuffer", "centroid",
        "class", "column_major", "compile", "compile_fragment", "compileshader", "const",
        "consumestructuredbuffer", "continue", "default", "depthstencilstate",
        "depthstencilview", "discard", "do", "double", "domainshader", "dword", "else",
        "export", "extern", "false", "float", "for", "fxgroup", "geometryshader",
        "groupshared", "half", "hullshader", "if", "in", "inline", "inout",
        "inputpatch", "int", "interface", "line", "lineadj", "linear", "linestream",
        "matrix", "min16float", "min10float", "min16int", "min12int", "min16uint",
        "namespace", "nointerpolation", "noperspective", "null", "out", "outputpatch",
        "packoffset", "pass", "pixelfragment", "pixelshader", "point", "pointstream",
        "precise", "rasterizerstate", "rendertargetview", "return", "register",
        "row_major", "rwbuffer", "rwbyteaddressbuffer", "rwstructuredbuffer",
        "rwtexture1d", "rwtexture1darray", "rwtexture2d", "rwtexture2darray",
        "rwtexture3d", "sample", "sampler", "sampler1d", "sampler2d", "sampler3d",
        "samplercube", "samplerstate", "samplercomparisonstate", "shared", "snorm",
        "stateblock", "stateblock_state", "static", "string", "struct", "switch",
        "structuredbuffer", "tbuffer", "technique", "technique10", "technique11",
        "texture", "texture1d", "texture1darray", "texture2d", "texture2darray",
        "texture2dms", "texture2dmsarray", "texture3d", "texturecube",
        "texturecubearray", "true", "typedef", "triangle", "triangleadj",
        "trianglestream", "uint", "uniform", "unorm", "unsigned", "vector",
        "vertexfragment", "vertexshader", "void", "volatile", "while",
    ];

    public static string Escape(string name) => Keywords.Contains(name) ? name + "_" : name;
}
