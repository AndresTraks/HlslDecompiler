float4 value;

struct GS_IN
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float4 color2 : COLOR2;
    float sv_clipdistance : SV_ClipDistance;
};

struct GS_IN2
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float4 color2 : COLOR2;
    float sv_clipdistance : SV_ClipDistance;
};

struct GS_OUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float4 color3 : COLOR3;
    float sv_clipdistance : SV_ClipDistance;
};

[maxvertexcount(6)]
void main(triangle GS_IN input[3], triangle GS_IN2 input2[3],
            inout TriangleStream<GS_OUT> lineStream)
{
    lineStream.RestartStrip();
    lineStream.Append(input[0]);
    lineStream.Append(input[1]);
    
    GS_OUT o = (GS_OUT) 0;
    o.color = input2[2].position;
    lineStream.Append(o);
}
