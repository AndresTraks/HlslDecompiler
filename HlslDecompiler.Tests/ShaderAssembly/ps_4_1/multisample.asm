ps_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms(4) (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 4
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
mov r1, l(0, 0, 0, 0)
mov r2.x, l(0)
loop
ige r2.y, r2.x, l(4)
breakc_nz r2.y
ldms r3, r0, t0, r2.x
add r1, r1, r3
iadd r2.x, r2.x, l(1)
endloop
mul o0, r1, l(0.25, 0.25, 0.25, 0.25)
ret
