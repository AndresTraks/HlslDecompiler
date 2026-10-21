ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 2
mul r0.x, v1.y, v1.x
fcall fp0[0][0]
mov o0.xyz, r0.yzw
mov o0.w, l(1)
ret
label fb0
mov r1.xy, this[0].yx
mad r0.yzw, v0.zyx, cb[r1.y][r1.x].xxx, -r0.xxx
ret
label fb1
mov r1.xy, this[0].yx
mad r0.yzw, v0.xyz, cb[r1.y][r1.x].xyz, r0.xxx
ret
