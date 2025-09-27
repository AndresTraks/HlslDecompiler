int address;
int address2;
float4 floats[8];

float4 main(float2 uv : TEXCOORD) : COLOR
{
    int idx = (int) (uv.x * 8.0);
    return uv[address];
}
