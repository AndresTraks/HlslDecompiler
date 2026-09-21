vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
dcl_constantbuffer CB1[134], dynamicIndexed
dcl_input v0.xyz
dcl_input v1.xyz
dcl_input v2.xy
dcl_input v3
dcl_input v4
dcl_input_sgv v5.x, instance_id
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2.xy
dcl_output o2.z
dcl_output o3
dcl_temps 6
utof r0.x, v5.x
mad r0.y, r0.x, l(0.5), cb0[4].w
mul r0.x, r0.x, l(0.617999971)
frc r0.x, r0.x
mad r0.x, r0.x, l(0.5), l(0.5)
mul o3, r0.x, cb1[132]
sincos r0.x, null, r0.y
iadd r1, v3, cb1[133].x
ishl r1, r1, l(2, 2, 2, 2)
mul r2, v4.y, cb1[r1.y + 7]
mad r2, cb1[r1.x + 7], v4.x, r2
mad r2, cb1[r1.z + 7], v4.z, r2
mad r2, cb1[r1.w + 7], v4.w, r2
mov r3.xyz, v0.xyz
mov r3.w, l(1)
dp4 r2.w, r3, r2
mul r4, v4.y, cb1[r1.y + 4]
mad r4, cb1[r1.x + 4], v4.x, r4
mad r4, cb1[r1.z + 4], v4.z, r4
mad r4, cb1[r1.w + 4], v4.w, r4
dp4 r2.x, r3, r4
dp3 r4.x, v1.xyz, r4.xyz
mul r5, v4.y, cb1[r1.y + 5]
mad r5, cb1[r1.x + 5], v4.x, r5
mad r5, cb1[r1.z + 5], v4.z, r5
mad r5, cb1[r1.w + 5], v4.w, r5
dp4 r2.y, r3, r5
dp3 r4.y, v1.xyz, r5.xyz
mul r5, v4.y, cb1[r1.y + 6]
mad r5, cb1[r1.x + 6], v4.x, r5
mad r5, cb1[r1.z + 6], v4.z, r5
mad r1, cb1[r1.w + 6], v4.w, r5
dp4 r2.z, r3, r1
dp3 r4.z, v1.xyz, r1.xyz
dp4 r0.y, r2, cb1[1]
mad r0.y, r0.x, l(0.100000001), r0.y
dp4 r0.w, r2, cb1[3]
dp4 r0.x, r2, cb1[0]
dp4 r0.z, r2, cb1[2]
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
add r0.xyz, r0.xyz, -cb0[4].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
mad r0.x, -r0.x, l(0.00999999978), l(1)
max o2.z, r0.x, l(0)
dp3 r0.x, r4.xyz, cb1[0].xyz
dp3 r0.y, r4.xyz, cb1[1].xyz
dp3 r0.z, r4.xyz, cb1[2].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul o1.xyz, r0.www, r0.xyz
mul r0.x, cb0[4].w, l(0.00999999978)
mov r0.y, l(0)
add o2.xy, r0.xy, v2.xy
ret
