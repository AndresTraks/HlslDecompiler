ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 3
mov r0.xyz, v0.xyz
mov r0.w, l(0)
loop
ige r1.x, r0.w, cb0[0].x
breakc_nz r1.x
fcall fp0[0][0]
mul r0.xyz, r1.xyz, l(0.5, 0.5, 0.5, 0)
iadd r0.w, r0.w, l(1)
endloop
mov o0.xyz, r0.xyz
mov o0.w, l(1)
ret
label fb0
mov r1.w, this[0].y
mov r2.x, this[0].x
add r1.xyz, r0.zyx, cb[r2.x][r1.w].xxx
ret
label fb1
mov r1.w, this[0].y
mov r2.x, this[0].x
mul r1.xyz, r0.xyz, cb[r2.x][r1.w].xyz
ret
