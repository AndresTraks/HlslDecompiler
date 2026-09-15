vs_3_0
def c7, -2, 3, 0, 0
dcl_position v0
dcl_normal v1
dcl_position o0
dcl_texcoord o1.xyz
dcl_texcoord1 o2
dp4 o0.x, v0, c0
dp4 o0.y, v0, c1
dp4 o0.z, v0, c2
dp4 o0.w, v0, c3
mul r0.xyz, c4.yzx, v1.zxy
mad o1.xyz, v1.yzx, c4.zxy, -r0.xyz
dp4 r0.x, v0, c5
slt r0.y, -r0.x, r0.x
slt r0.z, r0.x, -r0.x
mov_sat r0.x, r0.x
add o2.x, -r0.z, r0.y
dp4 r0.y, v0, c6
slt r0.z, -r0.y, r0.y
slt r0.y, r0.y, -r0.y
add o2.y, -r0.y, r0.z
add r0.yzw, -c4.xyz, v0.xyz
dp3 r0.y, r0.yzw, r0.yzw
rsq r0.y, r0.y
rcp o2.z, r0.y
mad r0.y, r0.x, c7.x, c7.y
mul r0.x, r0.x, r0.x
mul o2.w, r0.x, r0.y
