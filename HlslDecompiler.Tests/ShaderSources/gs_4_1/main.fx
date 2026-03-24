struct GS_IN
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

struct GS_IN2
{
    float4 position : SV_POSITION;
    float4 color2 : COLOR2;
};

[maxvertexcount(6)]
void main(triangle GS_IN input[3], triangle GS_IN input2[3],
            inout LineStream<GS_IN2> lineStream)
{
    lineStream.RestartStrip();
    lineStream.Append(input[0]);
    lineStream.Append(input2[1]);
}
