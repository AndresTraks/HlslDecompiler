using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public enum D3D10Primitive
    {
        Undefined = 0,
        Point = 1,
        Line = 2,
        Triangle = 3,
        LineAdj = 6,
        TriangleAdj = 7,
        ControlPointPatch1 = 8,
        ControlPointPatch2 = 9,
        ControlPointPatch3 = 10,
        ControlPointPatch4 = 11,
        ControlPointPatch5 = 12,
        ControlPointPatch6 = 13,
        ControlPointPatch7 = 14,
        ControlPointPatch8 = 15,
        ControlPointPatch9 = 16,
        ControlPointPatch10 = 17,
        ControlPointPatch11 = 18,
        ControlPointPatch12 = 19,
        ControlPointPatch13 = 20,
        ControlPointPatch14 = 21,
        ControlPointPatch15 = 22,
        ControlPointPatch16 = 23,
        ControlPointPatch17 = 24,
        ControlPointPatch18 = 25,
        ControlPointPatch19 = 26,
        ControlPointPatch20 = 27,
        ControlPointPatch21 = 28,
        ControlPointPatch22 = 29,
        ControlPointPatch23 = 30,
        ControlPointPatch24 = 31,
        ControlPointPatch25 = 32,
        ControlPointPatch26 = 33,
        ControlPointPatch27 = 34,
        ControlPointPatch28 = 35,
        ControlPointPatch29 = 36,
        ControlPointPatch30 = 37,
        ControlPointPatch31 = 38,
        ControlPointPatch32 = 39
    }
}

public static class D3D10PrimitiveExtensions
{
    public static String ToHlslString(this D3D10Primitive primitive)
    {
        return primitive switch
        {
            D3D10Primitive.Point => "point",
            D3D10Primitive.Line => "line",
            D3D10Primitive.Triangle => "triangle",
            D3D10Primitive.LineAdj => "line_adj",
            D3D10Primitive.TriangleAdj => "triangle_adj",
            _ => throw new NotImplementedException(primitive.ToString()),
        };
    }
}
