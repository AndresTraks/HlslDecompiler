vs_3_0
def c4, 1, 0, 0, 0
dcl_position v0
dcl_texcoord v1
dcl_2d s0
dcl_position o0
mul r0, c4.xxyy, v1.xyxx
texldl r0, r0, s0
mad r0, r0.x, c4.yxyy, v0
dp4 o0.x, r0, c0
dp4 o0.y, r0, c1
dp4 o0.z, r0, c2
dp4 o0.w, r0, c3
