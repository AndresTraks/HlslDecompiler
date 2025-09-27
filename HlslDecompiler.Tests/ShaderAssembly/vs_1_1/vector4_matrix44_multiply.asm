vs_1_1
def c4, 5, 2, 3, 4
def c5, 1, 0, 0, 0
dcl_position v0
dp4 oPos.x, v0, c0
dp4 oPos.y, v0, c1
dp4 oPos.z, v0, c2
dp4 oPos.w, v0, c3
dp4 oT1.x, v0.yxzw, c0
dp4 oT1.y, v0.yxzw, c1
dp4 oT1.z, v0.yxzw, c2
dp4 oT1.w, v0.yxzw, c3
max r0, -v0.yxzw, v0.yxzw
dp4 oT2.x, r0, c0
dp4 oT2.y, r0, c1
dp4 oT2.z, r0, c2
dp4 oT2.w, r0, c3
mul r0, v0, c4
dp4 oT3.x, r0, c0
dp4 oT3.y, r0, c1
dp4 oT3.z, r0, c2
dp4 oT3.w, r0, c3
mad r0, v0.xyzx, c5.xxxy, c5.yyyx
dp4 oT4.x, r0, c0
dp4 oT4.y, r0, c1
dp4 oT4.z, r0, c2
dp4 oT4.w, r0, c3
