sampler2D diffuse;
float4 wave;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half2 t0 = float2(cos((half)wave.x), sin((half)wave.x));
	return half4((half2)t0.yx, float2(0, 1)) * (half4)(tex2D(diffuse, texcoord));
}
