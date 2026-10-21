ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_function_table ft2 = {fb2}
dcl_function_table ft3 = {fb3}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_interface fp1[1][1] = {ft2, ft3}
dcl_input_ps linear v0.xyz
dcl_input_ps linear v1.xyz
dcl_output o0
dcl_temps 2
mul r0.xyz, v0.xyz, v1.xxx
fcall fp0[0][0]
fcall fp1[0][0]
mov o0.xyz, r0.xyz
mov o0.w, l(1)
ret
label fb0
mov r0.w, this[0].y
mov r1.w, this[0].x
mad r1.xyz, r0.zyx, cb[r1.w][r0.w].xxx, -v1.yyy
ret
label fb1
mov r0.w, this[0].y
mov r1.w, this[0].x
mad r1.xyz, r0.xyz, cb[r1.w][r0.w].xyz, v1.yyy
ret
label fb2
mov r0.w, this[1].y
mov r1.w, this[1].x
mad r0.xyz, r1.zyx, cb[r1.w][r0.w].xxx, -v1.zzz
ret
label fb3
mov r0.w, this[1].y
mov r1.w, this[1].x
mad r0.xyz, r1.xyz, cb[r1.w][r0.w].xyz, v1.zzz
ret
