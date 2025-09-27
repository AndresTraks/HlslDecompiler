vs_1_1
def c4, 5, 2, 3, 4
dcl_position v0
mul r0, v0.y, c1
mad r0, c0, v0.x, r0
mad r0, c2, v0.z, r0
mad oPos, c3, v0.w, r0
add oT4, r0, c3
mul r0, v0.x, c1
mad r0, c0, v0.y, r0
mad r0, c2, v0.z, r0
mad oT1, c3, v0.w, r0
max r0, -v0.yxzw, v0.yxzw
mul r1, r0.y, c1
mad r1, c0, r0.x, r1
mad r1, c2, r0.z, r1
mad oT2, c3, r0.w, r1
mul r0, v0, c4
mul r1, r0.y, c1
mad r1, c0, r0.x, r1
mad r1, c2, r0.z, r1
mad oT3, c3, r0.w, r1
