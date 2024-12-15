float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
    float4 r0 = 0;
    for (int i = 3; i < count; i++) {
        r0 += texcoord;
    }
    return r0;
}
