float count;
float count2;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
    float4 r0 = 0;
    float4 r1 = 0;
    for (int i = 3; i < count; i++) {
        for (int j = 5; j < count2; j++)
        {
            r1 += texcoord;
        }
        r0 += texcoord;
    }
    return r0 + r1;
}
