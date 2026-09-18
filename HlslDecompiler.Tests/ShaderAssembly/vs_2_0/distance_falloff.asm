vs_2_0
def c6, 1, 0, 0, 0
dcl_position v0
dcl_normal v1
dp4 oPos.x, v0, c0
dp4 oPos.y, v0, c1
dp4 oPos.z, v0, c2
dp4 oPos.w, v0, c3
add r0.xyz, -v0.xyz, c4.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
rcp r1.z, r0.w
dp3 r0.x, r0.xyz, v1.xyz
slt r0.y, -r0.x, r0.x
slt r0.x, r0.x, -r0.x
add r0.x, -r0.x, r0.y
mul r2.xy, r1.zz, r1.zz
mov r2.z, c6.x
mov r1.w, c6.x
mul r0.yzw, r1.zwz, r2.xyz
mov oD0.zw, r1.zw
dp3 r0.y, r0.yzw, c5.xyz
rcp r0.y, r0.y
mul oD0.x, r0.x, r0.y
max r0.x, r0.y, c6.y
min r0.x, r0.x, c6.x
log r0.x, r0.x
mul r0.x, r0.x, c4.w
exp oD0.y, r0.x
