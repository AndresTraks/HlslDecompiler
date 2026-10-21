ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 2
lt r0.x, l(0), v0.x
if_nz r0.x
fcall fp0[0][0]
else
mov r0.xyz, v0.xyz
endif
mov o0.xyz, r0.xyz
mov o0.w, l(1)
ret
label fb0
mov r0.w, this[0].y
mov r1.x, this[0].x
add r0.xyz, v0.xzy, cb[r1.x][r0.w].xxx
ret
label fb1
mov r0.w, this[0].y
mov r1.x, this[0].x
mul r0.xyz, v0.yzx, cb[r1.x][r0.w].xyz
ret
