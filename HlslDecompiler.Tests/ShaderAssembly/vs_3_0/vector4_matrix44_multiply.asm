vs_3_0
def c4, 5, 2, 3, 4
def c5, 1, 0, 0, 0
dcl_position v0
dcl_position o0
dcl_position1 o1
dcl_position2 o2
dcl_position3 o3
dcl_position4 o4
dp4 o0.x, v0, c0
dp4 o0.y, v0, c1
dp4 o0.z, v0, c2
dp4 o0.w, v0, c3
dp4 o1.x, v0.yxzw, c0
dp4 o1.y, v0.yxzw, c1
dp4 o1.z, v0.yxzw, c2
dp4 o1.w, v0.yxzw, c3
dp4 o2.x, v0.yxzw_abs, c0
dp4 o2.y, v0.yxzw_abs, c1
dp4 o2.z, v0.yxzw_abs, c2
dp4 o2.w, v0.yxzw_abs, c3
mul r0, c4, v0
dp4 o3.x, r0, c0
dp4 o3.y, r0, c1
dp4 o3.z, r0, c2
dp4 o3.w, r0, c3
mad r0, v0.xyzx, c5.xxxy, c5.yyyx
dp4 o4.x, r0, c0
dp4 o4.y, r0, c1
dp4 o4.z, r0, c2
dp4 o4.w, r0, c3
