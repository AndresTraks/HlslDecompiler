ps_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2dms(4) (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps_sgv constant v1.x, sampleIndex
dcl_output o0
dcl_temps 2
iadd r0.x, v1.x, l(1)
and r0.x, r0.x, l(3)
ftoi r1.xy, v0.xy
mov r1.zw, l(0, 0, 0, 0)
ldms r0, r1, t0, r0.x
ldms r1, r1.xyww, t0, v1.x
add r0, r0, -r1
mad r0, cb0[0].x, r0, r1
mul o0, r0, cb0[0].y
ret
