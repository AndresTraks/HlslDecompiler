sampler2D diffuse;
float3x3 transform;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	r0 = tex2D(diffuse, texcoord.xy);
	o.x = dot(r0.xyz, transpose(transform)[0].xyz);
	o.y = dot(r0.xyz, transpose(transform)[1].xyz);
	o.z = dot(r0.xyz, transpose(transform)[2].xyz);
	o.w = 1;

	return o;
}
