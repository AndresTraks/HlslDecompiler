vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyz
dcl_output o0.xyzw
dcl_output o1.xyz
dcl_output o2.xyz
dcl_output o3.xyz
dcl_temps 2
mul r0.xyz, v0.yyyy, cb0[1].xyzx
mad r1.xyz, cb0[0].xyzx, v0.xxxx, r0.xyzx
mad o0.xyz, cb0[2].xyzx, v0.zzzz, r1.xyzx
mov o0.w, l(1.000000)
mul r1.xyz, v0.xxxx, cb0[1].xyzx
mad r1.xyz, cb0[0].xyzx, v0.yyyy, r1.xyzx
mad o1.xyz, cb0[2].xyzx, v0.zzzz, r1.xyzx
mul r1.xyz, |v0.xxxx|, cb0[1].xyzx
mad r1.xyz, cb0[0].xyzx, |v0.yyyy|, r1.xyzx
mad o2.xyz, cb0[2].xyzx, |v0.zzzz|, r1.xyzx
add r0.w, v0.x, v0.x
mad r0.xyz, cb0[0].xyzx, r0.wwww, r0.xyzx
mul r0.w, v0.z, l(3.000000)
mad o3.xyz, cb0[2].xyzx, r0.wwww, r0.xyzx
ret
