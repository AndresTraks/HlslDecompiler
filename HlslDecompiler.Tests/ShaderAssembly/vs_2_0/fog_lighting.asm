vs_2_0
def c10, 0, 1, 0, 0
dcl_position v0
dcl_normal v1
dp4 oPos.x, v0, c0
dp4 oPos.y, v0, c1
dp4 oPos.z, v0, c2
dp3 r0.x, v1.xyz, c4.xyz
dp3 r0.y, v1.xyz, c5.xyz
dp3 r0.z, v1.xyz, c6.xyz
nrm r1.xyz, r0.xyz
dp3 r0.x, r1.xyz, -c7.xyz
max r0.x, r0.x, c10.x
min r0.x, r0.x, c10.y
mad oD0, c8, r0.x, c8.w
add r0.x, -c9.x, c9.y
rcp r0.x, r0.x
dp4 r0.y, v0, c3
add r0.z, -r0.y, c9.y
mul r0.x, r0.x, r0.z
max r0.x, r0.x, c10.x
min oFog, r0.x, c10.y
rcp r0.x, r0.y
mov oPos.w, r0.y
mul oPts, r0.x, c9.z
