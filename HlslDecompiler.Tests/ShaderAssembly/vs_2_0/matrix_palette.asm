vs_2_0
def c28, 3, 1, 0, 0
dcl_position v0
dcl_normal v1
dcl_blendweight v2
dcl_blendindices v3
mul r0.xy, v3.xy, c28.xx
mova a0.xy, r0.yx
dp4 r0.x, v0, c0[a0.x]
dp4 r0.y, v0, c1[a0.x]
dp4 r0.z, v0, c2[a0.x]
mul r0.xyz, r0.xyz, v2.yyy
dp4 r1.x, v0, c0[a0.y]
dp4 r1.y, v0, c1[a0.y]
dp4 r1.z, v0, c2[a0.y]
mad r0.xyz, r1.xyz, v2.xxx, r0.xyz
mov r0.w, c28.y
dp4 oPos.x, r0, c24
dp4 oPos.y, r0, c25
dp4 oPos.z, r0, c26
dp4 oPos.w, r0, c27
dp3 r0.x, v1.xyz, c0[a0.x].xyz
dp3 r0.y, v1.xyz, c1[a0.x].xyz
dp3 r0.z, v1.xyz, c2[a0.x].xyz
mul r0.xyz, r0.xyz, v2.yyy
dp3 r1.x, v1.xyz, c0[a0.y].xyz
dp3 r1.y, v1.xyz, c1[a0.y].xyz
dp3 r1.z, v1.xyz, c2[a0.y].xyz
mad r0.xyz, r1.xyz, v2.xxx, r0.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul o0.xyz, r0.www, r0.xyz
