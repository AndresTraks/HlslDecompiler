vs_4_0
dcl_constantbuffer cb0[8], immediateIndexed
dcl_input v0
dcl_input v1.xy
dcl_input v2.x
dcl_output_siv o0, position
dcl_output o1
dcl_temps 1
dp4 o0.x, v0, cb0[0]
dp4 o0.y, v0, cb0[1]
dp4 o0.z, v0, cb0[2]
dp4 o0.w, v0, cb0[3]
and r0.x, v1.y, l(255)
utof r0.x, r0.x
mul r0.x, r0.x, l(0.00392156886)
ushr r0.y, v1.x, l(4)
and r0.y, r0.y, l(3)
iadd r0.z, v2.x, l(1)
itof r0.z, r0.z
mad o1, cb0[0], r0.x, r0.z
ret
