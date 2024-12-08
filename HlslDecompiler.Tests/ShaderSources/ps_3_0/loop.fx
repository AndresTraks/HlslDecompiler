float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
    float4 o = 0;
    for (int i = 3; i < count; i++) {
        o += texcoord;
    }
    return o;
}
