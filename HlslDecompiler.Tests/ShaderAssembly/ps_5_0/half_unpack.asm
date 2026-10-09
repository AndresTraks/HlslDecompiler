ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps constant v0.xy
dcl_output o0
dcl_temps 2
ushr r0.xy, v0.xy, l(16, 16, 0, 0)
f16tof32 r0.zw, r0.xy
f16tof32 r0.xy, v0.xy
add r1.x, r0.w, r0.x
f32tof16 r1.x, r1.x
utof r1.x, r1.x
add o0, r0, r1.x
ret
