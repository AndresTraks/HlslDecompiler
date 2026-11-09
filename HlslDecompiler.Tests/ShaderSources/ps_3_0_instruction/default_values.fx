struct struct1
{
	float4 a;
	float b;
	float2 c;
	int d;
};

float3 m_arr3[2] : register(c19) = { float3(1, 2, 3), float3(4, 5, 6) };
float4 m_arr4[2] : register(c18) = { float4(1, 2, 3, 4), float4(5, 6, 7, 8) };
float m_array[3] = { 0.25, 0.5, 0.75 };
bool m_bool : register(c10) = true;
float2x4 m_col2x4 : register(c16) = float2x4(1, 2, 3, 4, 5, 6, 7, 8);
float3x4 m_col3x4 : register(c17) = float3x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
float4x3 m_colRect : register(c15) = float4x3(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
bool m_false : register(c12) = false;
int m_int : register(c9) = 2;
float4x4 m_matrix : register(c7) = 0;
int m_neg : register(c11) = -7;
float2x3 m_rect : register(c13) = float2x3(1, 2, 3, 4, 5, 6);
row_major float4x4 m_rowMatrix : register(c8) = 0;
row_major float2x3 m_rowRect : register(c14) = float2x3(1, 2, 3, 4, 5, 6);
float m_scalar : register(c4) = 0.300000012;
struct1 m_struct = { float4(1, 2, 3, 4), 5, float2(6, 7), 8 };
float4 m_vector : register(c5) = float4(0.200000003, 0.200000003, 0.200000003, 1);
float3 m_vector3 : register(c6) = float3(0.910000026, 0.910000026, 0.910000026);

float4 main(float texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float r0;
	r0 = m_scalar.x;
	r0 = r0.x + m_vector.x;
	r0 = r0.x + m_vector3.x;
	r0 = r0.x + transpose(m_matrix)[0].x;
	r0 = r0.x + m_rowMatrix[0].x;
	r0 = r0.x + m_array[1].x;
	r0 = r0.x + m_int.x;
	r0 = r0.x + m_bool.x;
	r0 = r0.x + m_neg.x;
	r0 = r0.x + m_false.x;
	r0 = r0.x + transpose(m_rect)[0].x;
	r0 = r0.x + m_rowRect[0].x;
	r0 = r0.x + transpose(m_colRect)[0].x;
	r0 = r0.x + transpose(m_col2x4)[0].x;
	r0 = r0.x + transpose(m_col3x4)[0].x;
	r0 = r0.x + m_arr4[0].x;
	r0 = r0.x + m_arr3[0].x;
	r0 = r0.x + m_struct.a.x;
	r0 = r0.x + m_struct.b;
	o = r0.x + texcoord.x;

	return o;
}
