vs_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input v0.xyz
dcl_input v1.x
dcl_output_siv o0, position
dcl_output o1
dcl_temps 2
mov r0.xyz, v0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ushr r0.x, v1.x, l(8)
ushr r0.y, v1.x, l(16)
and r0.xy, r0.xy, l(255, 255, 0, 0)
utof r0.xy, r0.xy
mul r0.yz, r0.xy, l(0, 0.00392156886, 0.00392156886, 0)
and r1.x, v1.x, l(255)
utof r1.x, r1.x
mul r0.x, r1.x, l(0.00392156886)
ushr r1.x, v1.x, l(24)
utof r1.x, r1.x
mul r0.w, r1.x, l(0.00392156886)
mul o1, r0.w, r0
ret
