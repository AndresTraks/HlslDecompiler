float4x4 decalMatrices[3];
uint decalCount;

SamplerState linearClamp;
Texture2DArray decalAlbedo;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int3 r2;
	float3 r3;
	if (decalCount != 0) {
		r0.xy = asint(i.texcoord.xy);
		r0.zw = int2(1065353216, 1065353216);
		r1.x = asint(dot(asfloat(r0.xyww), transpose(decalMatrices[0])[0]));
		r1.y = asint(dot(asfloat(r0.xyww), transpose(decalMatrices[0])[1]));
		r1.z = asint(dot(asfloat(r0.xyww), transpose(decalMatrices[0])[2]));
		r2 = (abs(asfloat(r1.xyz)) < float3(0.5, 0.5, 0.5)) ? -1 : 0;
		r1.z = r2.y & r2.x;
		r1.z = r2.z & r1.z;
		if (r1.z != 0) {
			r1.xy = asint(asfloat(r1.xy) + float2(0.5, 0.5));
			r1.z = 0;
			r1 = asint(decalAlbedo.Sample(linearClamp, asfloat(r1.xyz)));
			r1.xyz = asint(asfloat(r1.xyz) + -(i.color.xyz));
			r1.xyz = asint(asfloat(r1.www) * asfloat(r1.xyz) + i.color.xyz);
		} else {
			r1.xyz = asint(i.color.xyz);
		}
		r2.x = (1 >= decalCount) ? -1 : 0;
		if (r2.x == 0) {
			r3.x = dot(asfloat(r0.xyww), transpose(decalMatrices[1])[0]);
			r3.y = dot(asfloat(r0.xyww), transpose(decalMatrices[1])[1]);
			r3.z = dot(asfloat(r0), transpose(decalMatrices[1])[2]);
			r0.xyz = (abs(r3.xyz) < float3(0.5, 0.5, 0.5)) ? -1 : 0;
			r0.x = r0.y & r0.x;
			r0.x = r0.z & r0.x;
			if (r0.x != 0) {
				r0.xy = asint(r3.xy + float2(0.5, 0.5));
				r0.z = 1065353216;
				r0 = asint(decalAlbedo.Sample(linearClamp, asfloat(r0.xyz)));
				r0.xyz = asint(-(asfloat(r1.xyz)) + asfloat(r0.xyz));
				r1.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz) + asfloat(r1.xyz));
			}
		}
	} else {
		r1.xyz = asint(i.color.xyz);
		r2.x = -1;
	}
	if (r2.x == 0) {
		r0.x = (2 < decalCount) ? -1 : 0;
		if (r0.x != 0) {
			r0.xy = asint(i.texcoord.xy);
			r0.zw = int2(1065353216, 1065353216);
			r2.x = asint(dot(asfloat(r0.xyww), transpose(decalMatrices[2])[0]));
			r2.y = asint(dot(asfloat(r0.xyww), transpose(decalMatrices[2])[1]));
			r2.z = asint(dot(asfloat(r0), transpose(decalMatrices[2])[2]));
			r0.xyz = (abs(asfloat(r2.xyz)) < float3(0.5, 0.5, 0.5)) ? -1 : 0;
			r0.x = r0.y & r0.x;
			r0.x = r0.z & r0.x;
			if (r0.x != 0) {
				r0.xy = asint(asfloat(r2.xy) + float2(0.5, 0.5));
				r0.z = 1073741824;
				r0 = asint(decalAlbedo.Sample(linearClamp, asfloat(r0.xyz)));
				r0.xyz = asint(-(asfloat(r1.xyz)) + asfloat(r0.xyz));
				r1.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz) + asfloat(r1.xyz));
			}
		}
	}
	r1.w = asint(i.color.w);
	o = asfloat(r1);

	return o;
}
