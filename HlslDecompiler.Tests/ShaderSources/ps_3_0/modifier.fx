half4 main(centroid half4 texcoord : TEXCOORD) : COLOR
{
    return half4(saturate(texcoord));
}
