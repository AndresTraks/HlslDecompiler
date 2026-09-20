float4 packing;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	int2 t0 = asint(source.Sample(samp, texcoord).xy * packing.x);
	int4 t1 = int4(t0.y & int2(8388607, 2147483647), asint(source.Sample(samp, texcoord).x * packing.x) & int2(8388607, 2147483647));
	int2 t2 = 113 - (((uint2)t0.yx >> 23) & 255);
	int t3 = ((((uint)t0.y >> 16) & 32768) + (((t0.y & 2139095040) == 2139095040 ? t1.x ? ((t0.y | (((uint)t0.y >> 13) | ((uint)t0.y >> 3))) & 1023) + 31744 : 31744 : t1.y > 1207951360 ? 31743 : (uint)t1.y < 947912704 ? (t2.x < 24 ? (uint)t1.x + 8388608 >> t2.x : 0) >> 13 : (uint)t1.y - 939524096 >> 13) & 32767)) * 65536;
	int t4 = (uint)t3 >> 16;
	int2 t5 = t4 & int2(31744, 1023);
	int t6 = (t4 * 8192) & 8380416;
	int t7 = (uint)t5.y >> 8;
	uint t8 = t7 ? t7 : t5.y;
	uint t9 = t8 >> 4;
	uint t10 = t9 ? t9 : t8;
	uint t11 = t10 >> 2;
	int t12 = t9 ? t7 ? 12 : 4 : t7 ? 8 : 0;
	int t13 = t11 ? t12 + 2 : t12;
	int t14 = t5.y ? 10 - ((t11 ? t11 : t10) >> 1 ? t13 + 1 : t13) : 11;
	int t15 = (((uint)t0.x >> 16) & 32768) + (((t0.x & 2139095040) == 2139095040 ? t1.z ? ((t0.x | (((uint)t0.x >> 13) | ((uint)t0.x >> 3))) & 1023) + 31744 : 31744 : t1.w > 1207951360 ? 31743 : (uint)t1.w < 947912704 ? (t2.y < 24 ? (uint)t1.z + 8388608 >> t2.y : 0) >> 13 : (uint)t1.w - 939524096 >> 13) & 32767);
	int t16 = t3 + t15;
	int3 t17 = t16 & int3(31744, 65535, 1023);
	int t18 = (t17.y * 8192) & 8380416;
	int t19 = (uint)t17.z >> 8;
	uint t20 = t19 ? t19 : t17.z;
	uint t21 = t20 >> 4;
	uint t22 = t21 ? t21 : t20;
	uint t23 = t22 >> 2;
	int t24 = t21 ? t19 ? 12 : 4 : t19 ? 8 : 0;
	int t25 = t23 ? t24 + 2 : t24;
	int t26 = t17.z ? 10 - ((t23 ? t23 : t22) >> 1 ? t25 + 1 : t25) : 11;
	return float4(asfloat(((t15 * 65536) & -2147483648) + ((t17.x ? t17.x == 31744 ? t18 + 2139095040 : ((((uint)t17.y >> 10) * 8388608) & 260046848) + 939524096 + t18 : t17.z ? 947912704 - (t26 * 8388608) + (((t17.z << t26) * 8192) & 8380416) : 0) & 2147475456)), asfloat((t3 & -2147483648) + ((t5.x ? t5.x == 31744 ? t6 + 2139095040 : ((((uint)t4 >> 10) * 8388608) & 260046848) + 939524096 + t6 : t5.y ? 947912704 - (t14 * 8388608) + (((t5.y << t14) * 8192) & 8380416) : 0) & 2147475456)), asfloat(t16) * packing.y, 1);
}
