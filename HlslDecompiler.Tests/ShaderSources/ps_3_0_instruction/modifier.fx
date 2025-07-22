half4 main(centroid half4 texcoord : TEXCOORD) : COLOR
{
    float4 o;

    o = half4(saturate(texcoord));

    return o;
}
