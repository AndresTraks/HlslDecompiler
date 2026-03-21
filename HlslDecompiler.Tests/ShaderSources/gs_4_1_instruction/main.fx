struct GS_IN
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

[maxvertexcount(6)]
void main(triangle GS_IN input[3],
            inout LineStream<GS_IN> lineStream)
{
}
